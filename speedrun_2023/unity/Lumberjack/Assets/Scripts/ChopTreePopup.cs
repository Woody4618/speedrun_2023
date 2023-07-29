using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen that lets you refill energy for sol 
/// </summary>
public class ChopTreePopup : BasePopup
{
    public Button Button;
    public GameObject LoadingSpinner;
    
    void Start()
    {
        Button.onClick.AddListener(OnRefillEnergyButtonClicked);
    }

    public override void Open(UiService.UiData uiData)
    {
        var refillUiData = (uiData as ChopTreePopupUiData);

        if (refillUiData == null)
        {
            Debug.LogError("Wrong ui data for nft list popup");
            return;
        }

        base.Open(uiData);
    }

    private async void OnRefillEnergyButtonClicked()
    {
        (uiData as ChopTreePopupUiData).OnClick?.Invoke();
        Close();
    }
}
