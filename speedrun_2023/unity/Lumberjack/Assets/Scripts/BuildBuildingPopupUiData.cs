using System;
using DefaultNamespace;
using Solana.Unity.SDK;
using SolPlay.Scripts.Services;

public class BuildBuildingPopupUiData : UiService.UiData
{
    public WalletBase Wallet;
    public Action<TileConfig> OnClick;

    public BuildBuildingPopupUiData(WalletBase wallet, Action<TileConfig> onClick)
    {
        OnClick = onClick;
        Wallet = wallet;
    }
}
