using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Frictionless;
using Lumberjack;
using Lumberjack.Accounts;
using Lumberjack.Program;
using Solana.Unity.Programs;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using SolPlay.Scripts.Services;
using UnityEngine;

public class LumberjackService : MonoBehaviour
{
    public PublicKey LumberjackProgramIdPubKey = new PublicKey("HsT4yX959Qh1vis8fEqoQdgrHEJuKvaWGtHoPcTjk4mJ");
    
    public const int TIME_TO_REFILL_ENERGY = 60;
    public const int MAX_ENERGY = 10;
    
    public static LumberjackService Instance { get; private set; }
    public static Action<PlayerData> OnPlayerDataChanged;
    public static Action<BoardAccount> OnBoardDataChanged;
    public static Action OnInitialDataLoaded;
    public bool IsAnyTransactionInProgress => transactionsInProgress > 0;
    public PlayerData CurrentPlayerData;
    public BoardAccount CurrentBoardAccount;

    private SessionWallet sessionWallet;
    private PublicKey PlayerDataPDA;
    private PublicKey BoardPDA;
    private bool _isInitialized;
    private LumberjackClient lumberjackClient;
    private int transactionsInProgress;
    
    private void Awake() 
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(this); 
        } 
        else 
        { 
            Instance = this; 
        }

        Web3.OnLogin += OnLogin;
    }

    private void OnDestroy()
    {
        Web3.OnLogin -= OnLogin;
    }

    private async void OnLogin(Account account)
    {
        var solBalance = await Web3.Instance.WalletBase.GetBalance(Commitment.Confirmed);
        if (solBalance < 20000)
        {
            Debug.Log("Not enough sol. Requsting airdrop");
            var result = await Web3.Instance.WalletBase.RequestAirdrop(commitment: Commitment.Confirmed);
            if (!result.WasSuccessful)
            {
                Debug.Log("Airdrop failed.");
            }
        }

        PublicKey.TryFindProgramAddress(new[]
                {Encoding.UTF8.GetBytes("player1"), account.PublicKey.KeyBytes},
            LumberjackProgramIdPubKey, out PlayerDataPDA, out byte bump);

        PublicKey.TryFindProgramAddress(new[]
                {Encoding.UTF8.GetBytes("board")},
            LumberjackProgramIdPubKey, out BoardPDA, out byte bump2);

        lumberjackClient = new LumberjackClient(Web3.Rpc, Web3.WsRpc, LumberjackProgramIdPubKey);
        ServiceFactory.Resolve<SolPlayWebSocketService>().Connect(Web3.WsRpc.NodeAddress.AbsoluteUri);
        await SubscribeToPlayerDataUpdates();

        sessionWallet = await SessionWallet.GetSessionWallet(LumberjackProgramIdPubKey, "ingame");
        OnInitialDataLoaded?.Invoke();
    }

    public bool IsInitialized()
    {
        return _isInitialized;
    }

    private async Task SubscribeToPlayerDataUpdates()
    {
        AccountResultWrapper<PlayerData> playerData = null;
        
        try
        {
            playerData = await lumberjackClient.GetPlayerDataAsync(PlayerDataPDA, Commitment.Confirmed);
            if (playerData.ParsedResult != null)
            {
                CurrentPlayerData = playerData.ParsedResult;
                OnPlayerDataChanged?.Invoke(playerData.ParsedResult);
            }
            
            _isInitialized = true;
        }
        catch (Exception e)
        {
            Debug.Log("Probably playerData not available " + e.Message);
        }

        AccountResultWrapper<BoardAccount> boardAccount = null;

        try
        {
            boardAccount = await lumberjackClient.GetBoardAccountAsync(BoardPDA, Commitment.Confirmed);
            if (boardAccount.ParsedResult != null)
            {
                CurrentBoardAccount = boardAccount.ParsedResult;
                OnBoardDataChanged?.Invoke(boardAccount.ParsedResult);
            }
            
            _isInitialized = true;
        }
        catch (Exception e)
        {
            Debug.Log("Probably playerData not available " + e.Message);
        }

        ServiceFactory.Resolve<SolPlayWebSocketService>().SubscribeToPubKeyData(PlayerDataPDA, result =>
        {
            var playerData = PlayerData.Deserialize(Convert.FromBase64String(result.result.value.data[0]));
            Debug.Log("Player data socket " + playerData.Energy + " energy");
            CurrentPlayerData = playerData;
            OnPlayerDataChanged?.Invoke(playerData);
        });
        
        ServiceFactory.Resolve<SolPlayWebSocketService>().SubscribeToPubKeyData(BoardPDA, result =>
        {
            var boardAccount = BoardAccount.Deserialize(Convert.FromBase64String(result.result.value.data[0]));
            Debug.Log("Player data socket " + boardAccount.Wood + " wood");
            CurrentBoardAccount = boardAccount;
            OnBoardDataChanged?.Invoke(boardAccount);
        });
    }

    public async Task<RequestResult<string>> InitGameDataAccount(bool useSession)
    {
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash()
        };

        InitPlayerAccounts accounts = new InitPlayerAccounts();
        accounts.Player = PlayerDataPDA;
        accounts.Board = BoardPDA;
        accounts.Signer = Web3.Account;
        accounts.SystemProgram = SystemProgram.ProgramIdKey;

        var initTx = LumberjackProgram.InitPlayer(accounts, LumberjackProgramIdPubKey);
        tx.Add(initTx);

        if (useSession)
        {
            if (!(await sessionWallet.IsSessionTokenInitialized()))
            {
                var topUp = true;

                var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                accounts.Signer = Web3.Account.PublicKey;
                tx.Add(createSessionIX);
                Debug.Log("Has no session -> partial sign");
                tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
            }
        }

        var initResult =  await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
        Debug.Log(initResult.RawRpcResponse);
        await Web3.Rpc.ConfirmTransaction(initResult.Result, Commitment.Confirmed);
        await SubscribeToPlayerDataUpdates();
        return initResult;
    }

    public async Task<SessionWallet> RevokeSession()
    {
        await sessionWallet.PrepareLogout();
        sessionWallet.Logout();
        return sessionWallet;
    }

    public async void ChopTree(bool useSession)
    {
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash(maxSeconds:1)
        };

        ChopTreeAccounts accounts = new ChopTreeAccounts();
        accounts.Player = PlayerDataPDA;
        accounts.Board = BoardPDA;

        if (useSession)
        {
            if (!(await sessionWallet.IsSessionTokenInitialized()))
            {
                var topUp = true;

                var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                accounts.Signer = Web3.Account.PublicKey;
                tx.Add(createSessionIX);
                var chopInstruction = LumberjackProgram.ChopTree(accounts, 0, 0, LumberjackProgramIdPubKey);
                tx.Add(chopInstruction);
                Debug.Log("Has no session -> partial sign");
                tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
                SendAndConfirmTransaction(Web3.Wallet, tx, "Chop Tree and init session");
            }
            else
            {
                tx.FeePayer = sessionWallet.Account.PublicKey;
                accounts.SessionToken = sessionWallet.SessionTokenPDA;
                accounts.Signer = sessionWallet.Account.PublicKey;
                Debug.Log("Has session -> sign and send session wallet");
                var chopInstruction = LumberjackProgram.ChopTree(accounts, 0, 0, LumberjackProgramIdPubKey);
                tx.Add(chopInstruction);
                SendAndConfirmTransaction(sessionWallet, tx, "Chop Tree");
            }
        }
        else
        {
            tx.FeePayer = Web3.Account.PublicKey;
            accounts.Signer = Web3.Account.PublicKey;
            var chopInstruction = LumberjackProgram.ChopTree(accounts, 0, 0, LumberjackProgramIdPubKey);
            tx.Add(chopInstruction);
            Debug.Log("Sign without session");
            SendAndConfirmTransaction(Web3.Wallet, tx, "Chop Tree without session");
        }
    }
    
    private async void SendAndConfirmTransaction(WalletBase wallet, Transaction transaction, string label = "")
    {
        transactionsInProgress++;
        var res=  await wallet.SignAndSendTransaction(transaction, commitment: Commitment.Confirmed);
        if (res.WasSuccessful && res.Result != null)
        {
            await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
        }
        Debug.Log($"Send tranaction {label} with response: {res.RawRpcResponse}");
        transactionsInProgress--;
    }
}
