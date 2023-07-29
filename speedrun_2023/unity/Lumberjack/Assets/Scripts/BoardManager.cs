using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Frictionless;
using Lumberjack.Accounts;
using Lumberjack.Types;
using Solana.Unity.SDK;
using Solana.Unity.SDK.Nft;
using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
using Unity.VisualScripting;
using UnityEngine;

namespace DefaultNamespace
{
    public class BoardManager : MonoBehaviour
    { 
        public const int WIDTH = 10;
        public const int HEIGHT = 10;

        public const int ACTION_TYPE_CHOP = 0;
        public const int ACTION_TYPE_BUILD = 1;
        public const int ACTION_TYPE_UPGRADE = 2;
        public const int ACTION_TYPE_COLLECT = 3;
        
        public Tile TilePrefab; 
        public Cell CellPrefab; 
        public Cell[,] AllCells = new Cell[WIDTH, HEIGHT];
        public List<Tile> tiles = new List<Tile>();
        public TileConfig[] tileConfigs;
        public TextBlimp3D CoinBlimpPrefab;
        
        public bool IsWaiting;
        
        private bool isInitialized;
        private Dictionary<ulong, GameAction> alreadyPrerformedGameActions = new Dictionary<ulong, GameAction>();
        private bool HasPlayedInitialAnimations = false;

        private void Awake()
        {
            ServiceFactory.RegisterSingleton(this);
        }

        private void Start()
        {
            LumberjackService.OnPlayerDataChanged += OnPlayerDataChange;
            LumberjackService.OnBoardDataChanged += OnBoardDataChange;
            LumberjackService.OnGameActionHistoryChanged += OnGameActionHistoryChange;

            // Crete Cells
            for (int i = 0; i < WIDTH; i++)
            {
                for (int j = 0; j < HEIGHT; j++)
                {
                    Cell cellInstance = Instantiate(CellPrefab, transform);
                    cellInstance.transform.position = new Vector3(1.1f * i, 0, -1.1f * j);
                    cellInstance.Init(i, j, null);
                    AllCells[i,j] = cellInstance;
                } 
            }
        }

        private void OnDestroy()
        {
            LumberjackService.OnBoardDataChanged -= OnBoardDataChange;
        }

        private void OnPlayerDataChange(PlayerData obj)
        {
            // Nothing to do here? O.O
        }

        private void OnGameReset()
        {
            isInitialized = false;
            foreach (Tile tile in tiles)
            {
                Destroy(tile.gameObject);
            }
            tiles.Clear();
            
            for (int i = 0; i < WIDTH; i++)
            {
                for (int j = 0; j < HEIGHT; j++)
                {
                    AllCells[i, j].Tile = null;
                }
            }
        }

        private void OnBoardDataChange(BoardAccount playerData)
        {
            SetData(playerData);
        }
        
        private async void OnGameActionHistoryChange(GameActionHistory gameActionHistory)
        {
            if (!HasPlayedInitialAnimations)
            {
                foreach (GameAction gameAction in gameActionHistory.GameActions)
                {
                    if (gameAction.ActionId == 0)
                    {
                        continue;
                    }
                    alreadyPrerformedGameActions.Add(gameAction.ActionId, gameAction);
                }

                HasPlayedInitialAnimations = true;
                return;
            }
            
            foreach (GameAction gameAction in gameActionHistory.GameActions)
            {
                if (!alreadyPrerformedGameActions.ContainsKey(gameAction.ActionId))
                {
                    var targetCell = GetCell(gameAction.X, gameAction.Y);
                    
                    if (gameAction.ActionType == ACTION_TYPE_CHOP)
                    {
                        var tileConfig = FindTileConfigByNumber(gameAction.Tile);
                        targetCell.Tile.Init(tileConfig, gameAction.Tile, true);
                    }

                    if (gameAction.ActionType == ACTION_TYPE_BUILD)
                    {
                        var tileConfig = FindTileConfigByNumber(gameAction.Tile);
                        targetCell.Tile.Init(tileConfig, gameAction.Tile, true);
                    }
                    
                    if (gameAction.ActionType == ACTION_TYPE_UPGRADE)
                    {
                        var tileConfig = FindTileConfigByNumber(gameAction.Tile);
                        targetCell.Tile.Init(tileConfig, gameAction.Tile, true);
                    }
                    
                    if (gameAction.ActionType == ACTION_TYPE_COLLECT)
                    {
                        var blimp = Instantiate(CoinBlimpPrefab);

                        var tileConfig = FindTileConfigByNumber(gameAction.Tile);
                        blimp.SetData("5", null, tileConfig);
                        targetCell.Tile.Init(tileConfig, gameAction.Tile, true);
                        blimp.transform.position = targetCell.transform.position;
                        Nft nft = null;
                        try
                        {
                            var rpc = Web3.Wallet.ActiveRpcClient;
                            nft  = await Nft.TryGetNftData(gameAction.Avatar, rpc).AsUniTask();
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Could not load nft" + e);
                        }

                        if (nft == null)
                        {
                            nft = ServiceFactory.Resolve<NftService>().CreateDummyLocalNft(gameAction.Avatar);
                        }
                        
                        blimp.SetData("5", nft, tileConfig);
                        blimp.AddComponent<DestroyDelayed>();
                        Debug.Log("Is collectable: " + LumberjackService.IsCollectable(gameAction.Tile));
                    }
                    
                    alreadyPrerformedGameActions.Add(gameAction.ActionId, gameAction);
                }
            }   
        }

        public void SetData(BoardAccount playerData)
        {
            if (!isInitialized)
            {
                CreateStartingTiles(playerData);
                isInitialized = true;
            }
            else
            {
                //SpawnNewTile(playerData.NewTileX, playerData.NewTileY, playerData.NewTileLevel);
            }

            bool anyTileOutOfSync = false;
            // Compare tiles: 
            for (int i = 0; i < WIDTH; i++)
            {
                for (int j = 0; j < HEIGHT; j++)
                {
                    if (playerData.Data[j][i].BuildingType != 0 && GetCell(i, j).Tile == null)
                    {
                        anyTileOutOfSync = true;
                        Debug.LogWarning("Tiles out of sync.");
                    }else 
                    if (playerData.Data[j][i].BuildingType != GetCell(i, j).Tile.currentConfig.building_type)
                    {
                        anyTileOutOfSync = true;
                        Debug.LogWarning($"Tiles out of sync. x {i} y {j} from socket: {playerData.Data[j][i]} board: {GetCell(i, j).Tile.currentConfig.Number} ");
                    }
                } 
            }

            if (anyTileOutOfSync)
            {
                //RefreshFromPlayerdata(playerData);
                return;
            }
            
            IsWaiting = false;
        }

        public void RefreshFromPlayerdata(BoardAccount baordAccount)
        {
            OnGameReset();
            CreateStartingTiles(baordAccount);
            isInitialized = true;
            IsWaiting = false;
        }

        public Cell GetCell(int x, int y)
        {
            if (x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT) {
                return AllCells[x, y];
            } 
            
            return null;
        }

        public Cell GetAdjacentCell(Cell cell, Vector2Int direction)
        {
            int adjecentX = cell.X + direction.x;
            int adjecentY = cell.Y - direction.y;

            return GetCell(adjecentX, adjecentY);
        }

        private IEnumerator DestroyAfterSeconds(TextBlimp3D blimp)
        {
            yield return new WaitForSeconds(2);
            Destroy(blimp.gameObject);
        }

        private int IndexOf(TileConfig state)
        {
            for (int i = 0; i < tileConfigs.Length; i++)
            {
                if (state == tileConfigs[i]) {
                    return i;
                }
            }

            return -1;
        }
        
        private void CreateStartingTiles(BoardAccount playerData)
        {
            for (int x = 0; x < WIDTH; x++)
            {
                for (int y = 0; y < HEIGHT; y++)
                {
                    SpawnNewTile(x, y, playerData.Data[x][y]);
                } 
            }
        }

        private void SpawnNewTile(int i, int j, TileData tileData, Color? overrideColor = null)
        {
            var targetCell = GetCell(i, j);
            if (targetCell.Tile != null)
            {   
                // TODO: Refresh only the tiles that changed
                //Debug.LogError("Target cell already full: " + targetCell.Tile.currentConfig.Number);
                return;
            }
            
            // TODO: do we need sounds?
            /*if (SoundToggle.IsSoundEnabled())
            {
                SpawnAudioSource.PlayOneShot(SpawnClip);
            }*/
            
            Tile tileInstance = Instantiate(TilePrefab, transform);
            
            tileInstance.transform.position = targetCell.transform.position;
            TileConfig newConfig = FindTileConfigByNumber(tileData);
            if (overrideColor != null)
            {
                newConfig.MaterialColor = overrideColor.Value;
                //EditorUtility.SetDirty(newConfig);
            }

            tileInstance.Init(newConfig, tileData);
            tileInstance.Spawn(targetCell);
            tiles.Add(tileInstance);
        }
        
        private TileConfig FindTileConfigByNumber(TileData tileData)
        {
            foreach (var tileConfig in tileConfigs)
            {
                if (tileConfig.building_type == tileData.BuildingType)
                {
                    return tileConfig;
                }
            }

            return tileConfigs[tileConfigs.Length - 1];
        }
        
        public TileConfig FindTileConfigByName(string name)
        {
            foreach (var tileConfig in tileConfigs)
            {
                if (tileConfig.BuildingName == name)
                {
                    return tileConfig;
                }
            }

            return tileConfigs[tileConfigs.Length - 1];
        }
    }
}