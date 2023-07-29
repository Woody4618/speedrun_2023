using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeBuildingPopup : BasePopup
{
    public Button Button;
    public GameObject LoadingSpinner;
    
    void Start()
    {
        Button.onClick.AddListener(ButtonClicked);
    }

    public override void Open(UiService.UiData uiData)
    {
        var refillUiData = (uiData as UpgradeBuildingPopupUiData);

        if (refillUiData == null)
        {
            Debug.LogError("Wrong ui data for nft list popup");
            return;
        }

        base.Open(uiData);
    }

    private async void ButtonClicked()
    {
        (uiData as UpgradeBuildingPopupUiData).OnClick?.Invoke();
        Close();
    }
}
