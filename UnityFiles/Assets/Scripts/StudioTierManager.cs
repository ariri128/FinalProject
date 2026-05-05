using UnityEngine;
using TMPro;

public class StudioTierManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI studioTierText;

    private ResourceManager resourceManager;

    private string[] tierNames = {
        "Indie Developer",
        "Small Studio",
        "Large Studio",
        "AAA Studio"
    };

    // Revenue required to reach each tier (index matches tierNames)
    private float[] tierThresholds = { 0f, 500f, 5000f, 50000f };

    private int currentTier = 0;

    private float[] tierMultipliers = { 1f, 1.25f, 1.75f, 2.5f };

    private void Start()
    {
        resourceManager = FindObjectOfType<ResourceManager>();
        resourceManager.OnResourceChanged += HandleResourceChanged;
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (resourceManager != null)
            resourceManager.OnResourceChanged -= HandleResourceChanged;
    }

    private void HandleResourceChanged(ResourceType resourceType, float newValue)
    {
        if (resourceType != ResourceType.Revenue)
            return;

        for (int i = tierThresholds.Length - 1; i >= 0; i--)
        {
            if (newValue >= tierThresholds[i])
            {
                if (i != currentTier)
                {
                    currentTier = i;
                    UpdateUI();
                }
                break;
            }
        }
    }

    // Called by GameManager.ExecuteRelaunch()
    public void ResetTier()
    {
        currentTier = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (studioTierText != null)
            studioTierText.text = "Studio: " + tierNames[currentTier] + " (x" + tierMultipliers[currentTier].ToString("F2") + ")";
    }

    public float GetTierMultiplier()
    {
        return tierMultipliers[currentTier];
    }
}
