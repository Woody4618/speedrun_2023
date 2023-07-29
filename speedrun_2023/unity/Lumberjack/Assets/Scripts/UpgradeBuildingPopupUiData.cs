using System;
using Solana.Unity.SDK;
using SolPlay.Scripts.Services;

public class UpgradeBuildingPopupUiData : UiService.UiData
{
    public WalletBase Wallet;
    public Action OnClick;

    public UpgradeBuildingPopupUiData(WalletBase wallet, Action onClick)
    {
        Wallet = wallet;
        OnClick = onClick;
    }
}
