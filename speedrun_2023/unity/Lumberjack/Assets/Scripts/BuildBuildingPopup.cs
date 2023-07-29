using DefaultNamespace;
using Frictionless;
using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
using UnityEngine;
using UnityEngine.UI;


public class BuildBuildingPopup : BasePopup
{
    public Button SawMillButton;
    public Button StoneMineButton;
    public GameObject LoadingSpinner;
    
    void Start()
    {
        SawMillButton.onClick.AddListener(OnBuildSawMillButtonClicked);
        StoneMineButton.onClick.AddListener(OnBuildMineButtonClicked);
    }

    public override void Open(UiService.UiData uiData)
    {
        var refillUiData = (uiData as BuildBuildingPopupUiData);
        
        if (refillUiData == null)
        {
            Debug.LogError("Wrong ui data for nft list popup");
            return;
        }

        base.Open(uiData);
    }

    private async void OnBuildSawMillButtonClicked()
    {
        var config = ServiceFactory.Resolve<BoardManager>().FindTileConfigByName("SawMill");
        (uiData as BuildBuildingPopupUiData).OnClick(config);
        Close();
    }
    
    private async void OnBuildMineButtonClicked()
    {
        var config = ServiceFactory.Resolve<BoardManager>().FindTileConfigByName("StoneMine");
        (uiData as BuildBuildingPopupUiData).OnClick(config);
        Close();
    }
}
