using UnityEngine;
using UnityEngine.UI;

public class ReputationMilestoneChecker : MonoBehaviour
{
    [Header("UI References")]
    public Button seniorDevButton;
    public Button studioRelaunchButton;

    [Header("Thresholds")]
    public float seniorDevUnlockReputation = 10f;
    public float relaunchUnlockRevenue = 10000f;

    private ResourceManager resourceManager;

    private bool seniorDevUnlocked = false;
    private bool relaunchUnlocked = false;

    private void Start()
    {
        resourceManager = FindObjectOfType<ResourceManager>();
        resourceManager.OnResourceChanged += HandleResourceChanged;

        // Start both locked
        if (seniorDevButton != null)
            seniorDevButton.interactable = false;

        if (studioRelaunchButton != null)
            studioRelaunchButton.interactable = false;
    }

    private void OnDestroy()
    {
        if (resourceManager != null)
            resourceManager.OnResourceChanged -= HandleResourceChanged;
    }

    private void HandleResourceChanged(ResourceType resourceType, float newValue)
    {
        if (!seniorDevUnlocked && resourceType == ResourceType.Reputation)
        {
            if (newValue >= seniorDevUnlockReputation)
            {
                seniorDevUnlocked = true;

                if (seniorDevButton != null)
                    seniorDevButton.interactable = true;
            }
        }

        if (!relaunchUnlocked && resourceType == ResourceType.Revenue)
        {
            if (newValue >= relaunchUnlockRevenue)
            {
                relaunchUnlocked = true;

                if (studioRelaunchButton != null)
                    studioRelaunchButton.interactable = true;
            }
        }
    }

    // Called by GameManager after a relaunch to re-lock the relaunch button
    public void ResetRelaunchLock()
    {
        relaunchUnlocked = false;

        if (studioRelaunchButton != null)
            studioRelaunchButton.interactable = false;
    }
}
