using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System;
using System.IO;
using System.Xml.Serialization;

public class GameManager : MonoBehaviour
{
    public ResourceManager resourceManager;

    [Header("Stats UI")]
    public TextMeshProUGUI revenueText;
    public TextMeshProUGUI reputationText;
    public TextMeshProUGUI revenuePerSecondText;

    [Header("Generator UI")]
    public TextMeshProUGUI juniorDevCountText;
    public TextMeshProUGUI seniorDevCountText;
    public TextMeshProUGUI releasedGamesCountText;

    [Header("Upgrade UI")]
    public TextMeshProUGUI engineUpgradeStateText;
    public TextMeshProUGUI assetLibraryUpgradeStateText;
    public TextMeshProUGUI buildOptimizationUpgradeStateText;

    [Header("Upgrade Buttons")]
    public UnityEngine.UI.Button engineUpgradeButton;
    public UnityEngine.UI.Button assetLibraryUpgradeButton;
    public UnityEngine.UI.Button buildOptimizationUpgradeButton;

    [Header("Misc UI")]
    public TextMeshProUGUI statusMessageText;

    public float developGameClickValue = 10f;

    public List<Upgrade> upgrades = new List<Upgrade>();

    private Generator juniorDeveloperGenerator;
    private Generator seniorDeveloperGenerator;
    private Generator releasedGameGenerator;

    private float globalRevenueMultiplier = 1f;

    private string dataPath;
    private string upgradeFilePath;
    private string saveFilePath;
    private string playtimeFilePath;

    private float sessionStartTime;

    private StudioRelaunchManager relaunchManager;
    private ReputationMilestoneChecker milestoneChecker;

    private void Start()
    {
        if (resourceManager == null)
            resourceManager = GetComponent<ResourceManager>();

        relaunchManager = FindObjectOfType<StudioRelaunchManager>();
        milestoneChecker = FindObjectOfType<ReputationMilestoneChecker>();

        Debug.Log(Application.persistentDataPath);

        resourceManager.OnResourceChanged += HandleResourceChanged;

        InitializeFilePaths();
        CreatePlaytimeFileIfNeeded();

        juniorDeveloperGenerator = new JuniorDeveloperGenerator();
        seniorDeveloperGenerator = new SeniorDeveloperGenerator();
        releasedGameGenerator = new ReleasedGameGenerator();

        try
        {
            LoadUpgradeData();
            LoadGameState();
            statusMessageText.text = "Status: Save data loaded.";
        }
        catch (Exception exception)
        {
            statusMessageText.text = "Status: Load failed. Using default data.";
            CreateDefaultUpgradeFile();
            LoadUpgradeData();
            Debug.Log(exception.Message);
        }
        finally
        {
            UpdateUpgradeAvailability();
            UpdateRevenuePerSecond();
            UpdateUI();
        }

        sessionStartTime = Time.time;
    }

    private void Update()
    {
        RunPassiveIncome();
        UpdateGeneratorUI();
        UpdateUpgradeUI();
    }

    private void OnDestroy()
    {
        if (resourceManager != null)
            resourceManager.OnResourceChanged -= HandleResourceChanged;
    }

    private void HandleResourceChanged(ResourceType resourceType, float newValue)
    {
        UpdateResourceUI();
    }

    // ─── File Paths ───
    private void InitializeFilePaths()
    {
        dataPath = Application.persistentDataPath + "/GameStudioSimulatorData/";

        if (!Directory.Exists(dataPath))
            Directory.CreateDirectory(dataPath);

        upgradeFilePath = dataPath + "UpgradeData.xml";
        saveFilePath = dataPath + "SaveData.json";
        playtimeFilePath = dataPath + "SessionPlaytimes.txt";
    }

    private void CreatePlaytimeFileIfNeeded()
    {
        if (!File.Exists(playtimeFilePath))
            File.WriteAllText(playtimeFilePath, "Game Studio Simulator Session Playtimes\n");
    }

    // ─── Upgrade Data ───
    private void LoadUpgradeData()
    {
        if (!File.Exists(upgradeFilePath))
            CreateDefaultUpgradeFile();

        XmlSerializer serializer = new XmlSerializer(typeof(UpgradeFileCollection));

        using (FileStream stream = File.OpenRead(upgradeFilePath))
        {
            UpgradeFileCollection loadedData = (UpgradeFileCollection)serializer.Deserialize(stream);
            upgrades.Clear();

            for (int i = 0; i < loadedData.upgrades.Count; i++)
            {
                UpgradeFileData data = loadedData.upgrades[i];

                upgrades.Add(new Upgrade(
                    data.upgradeName,
                    data.cost,
                    new UpgradeEffect(data.multiplier, data.targetResourceType),
                    data.tier,
                    data.state
                ));
            }
        }
    }

    private void CreateDefaultUpgradeFile()
    {
        UpgradeFileCollection defaultData = new UpgradeFileCollection();

        defaultData.upgrades.Add(new UpgradeFileData(
            "Better Engine", 75f, 1.25f, ResourceType.RevenuePerSecond, 1, UpgradeState.Available));

        defaultData.upgrades.Add(new UpgradeFileData(
            "Asset Library", 150f, 1.5f, ResourceType.RevenuePerSecond, 2, UpgradeState.Locked));

        defaultData.upgrades.Add(new UpgradeFileData(
            "Build Optimization", 250f, 2f, ResourceType.RevenuePerSecond, 3, UpgradeState.Locked));

        XmlSerializer serializer = new XmlSerializer(typeof(UpgradeFileCollection));

        using (FileStream stream = File.Create(upgradeFilePath))
            serializer.Serialize(stream, defaultData);
    }

    // ─── Save / Load ───
    private void LoadGameState()
    {
        if (!File.Exists(saveFilePath))
            return;

        using (StreamReader stream = new StreamReader(saveFilePath))
        {
            string jsonString = stream.ReadToEnd();
            SaveData saveData = JsonUtility.FromJson<SaveData>(jsonString);

            if (saveData == null)
                return;

            resourceManager.SetResource(ResourceType.Revenue, saveData.revenue);
            resourceManager.SetResource(ResourceType.Reputation, saveData.reputation);

            juniorDeveloperGenerator.ownedCount = saveData.juniorDeveloperCount;
            seniorDeveloperGenerator.ownedCount = saveData.seniorDeveloperCount;
            releasedGameGenerator.ownedCount = saveData.releasedGameCount;

            for (int i = 0; i < upgrades.Count; i++)
            {
                upgrades[i].purchaseCount = 0;
                upgrades[i].state = UpgradeState.Locked;
            }

            for (int i = 0; i < saveData.purchasedUpgrades.Count && i < saveData.upgradePurchaseCounts.Count; i++)
            {
                Upgrade loaded = GetUpgradeByName(saveData.purchasedUpgrades[i]);
                if (loaded != null)
                {
                    loaded.purchaseCount = saveData.upgradePurchaseCounts[i];
                    loaded.state = loaded.IsFullyPurchased() ? UpgradeState.Purchased : UpgradeState.Available;
                }
            }

            RebuildGlobalRevenueMultiplier();
        }
    }

    private void SaveGameState()
    {
        SaveData saveData = new SaveData();

        saveData.revenue = resourceManager.GetResource(ResourceType.Revenue);
        saveData.reputation = resourceManager.GetResource(ResourceType.Reputation);
        saveData.juniorDeveloperCount = juniorDeveloperGenerator.ownedCount;
        saveData.seniorDeveloperCount = seniorDeveloperGenerator.ownedCount;
        saveData.releasedGameCount = releasedGameGenerator.ownedCount;

        saveData.purchasedUpgrades.Clear();
        saveData.upgradePurchaseCounts.Clear();

        for (int i = 0; i < upgrades.Count; i++)
        {
            saveData.purchasedUpgrades.Add(upgrades[i].upgradeName);
            saveData.upgradePurchaseCounts.Add(upgrades[i].purchaseCount);
        }

        string jsonString = JsonUtility.ToJson(saveData, true);

        using (StreamWriter stream = File.CreateText(saveFilePath))
            stream.WriteLine(jsonString);
    }

    // ─── Relaunch ───
    public void ExecuteRelaunch()
    {
        // Reset all resources
        resourceManager.SetResource(ResourceType.Revenue, 0f);
        resourceManager.SetResource(ResourceType.Reputation, 0f);
        resourceManager.SetResource(ResourceType.RevenuePerSecond, 0f);

        // Reset generators
        juniorDeveloperGenerator.ownedCount = 0;
        seniorDeveloperGenerator.ownedCount = 0;
        releasedGameGenerator.ownedCount = 0;

        // Reset upgrades
        for (int i = 0; i < upgrades.Count; i++)
        {
            upgrades[i].purchaseCount = 0;
            upgrades[i].state = i == 0 ? UpgradeState.Available : UpgradeState.Locked;
        }
        globalRevenueMultiplier = 1f;

        // Re-lock milestone buttons
        if (milestoneChecker != null)
            milestoneChecker.ResetRelaunchLock();

        FindObjectOfType<StudioTierManager>()?.ResetTier();

        UpdateUpgradeAvailability();
        UpdateRevenuePerSecond();
        UpdateUI();
        SaveGameState();

        statusMessageText.text = "Status: Studio relaunched! Permanent bonus active.";
    }

    // ─── Multipliers ───
    private float GetTotalMultiplier()
    {
        float permanent = relaunchManager != null ? relaunchManager.GetPermanentMultiplier() : 1f;
        float tier = FindObjectOfType<StudioTierManager>()?.GetTierMultiplier() ?? 1f;
        return globalRevenueMultiplier * permanent * tier;
    }

    private void RebuildGlobalRevenueMultiplier()
    {
        globalRevenueMultiplier = 1f;

        for (int i = 0; i < upgrades.Count; i++)
        {
            for (int j = 0; j < upgrades[i].purchaseCount; j++)
            {
                ApplyUpgradeEffect(upgrades[i].effect);
            }
        }
    }

    private Upgrade GetUpgradeByName(string upgradeName)
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].upgradeName == upgradeName)
                return upgrades[i];
        }
        return null;
    }

    // ─── Passive Income ───
    private void RunPassiveIncome()
    {
        float total = 0f;
        total += juniorDeveloperGenerator.Produce();
        total += seniorDeveloperGenerator.Produce();
        total += releasedGameGenerator.Produce();
        total *= GetTotalMultiplier();

        resourceManager.AddResource(ResourceType.Revenue, total * Time.deltaTime);
    }

    private void UpdateRevenuePerSecond()
    {
        float total = 0f;
        total += juniorDeveloperGenerator.Produce();
        total += seniorDeveloperGenerator.Produce();
        total += releasedGameGenerator.Produce();
        total *= GetTotalMultiplier();

        resourceManager.SetResource(ResourceType.RevenuePerSecond, total);
    }

    // ─── Upgrade Availability ───
    private void UpdateUpgradeAvailability()
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].IsFullyPurchased())
                continue;

            if (i == 0)
                upgrades[i].state = UpgradeState.Available;
            else if (upgrades[i - 1].purchaseCount >= 1)
                upgrades[i].state = UpgradeState.Available;
            else
                upgrades[i].state = UpgradeState.Locked;
        }
    }

    // ─── UI Updates ───
    private void UpdateUI()
    {
        UpdateResourceUI();
        UpdateGeneratorUI();
        UpdateUpgradeUI();
    }

    private void UpdateResourceUI()
    {
        revenueText.text = "Revenue: $" + NumberFormatter.Format(resourceManager.GetResource(ResourceType.Revenue));
        reputationText.text = "Reputation: " + NumberFormatter.Format(resourceManager.GetResource(ResourceType.Reputation));
        revenuePerSecondText.text = "Revenue / Sec: $" + NumberFormatter.Format(resourceManager.GetResource(ResourceType.RevenuePerSecond));
    }

    private void UpdateGeneratorUI()
    {
        juniorDevCountText.text = "Dev Count: " + juniorDeveloperGenerator.ownedCount;
        seniorDevCountText.text = "Senior Dev Count: " + seniorDeveloperGenerator.ownedCount;
        releasedGamesCountText.text = "Game Count: " + releasedGameGenerator.ownedCount;
    }

    private void UpdateUpgradeUI()
    {
        UpdateSingleUpgradeUI(0, engineUpgradeStateText, engineUpgradeButton);
        UpdateSingleUpgradeUI(1, assetLibraryUpgradeStateText, assetLibraryUpgradeButton);
        UpdateSingleUpgradeUI(2, buildOptimizationUpgradeStateText, buildOptimizationUpgradeButton);
    }

    private void UpdateSingleUpgradeUI(int index, TextMeshProUGUI label, UnityEngine.UI.Button button)
    {
        Upgrade u = upgrades[index];

        if (u.IsFullyPurchased())
        {
            label.text = "Purchased (" + u.purchaseCount + "/" + u.maxPurchases + ")";
            if (button != null) button.interactable = false;
        }
        else if (u.state == UpgradeState.Available)
        {
            label.text = "Available (" + u.purchaseCount + "/" + u.maxPurchases + ")";
            if (button != null) button.interactable = true;
        }
        else
        {
            label.text = "Locked";
            if (button != null) button.interactable = false;
        }
    }

    // ─── Public Button Handlers ───
    public void DevelopGame()
    {
        float clickAmount = developGameClickValue * GetTotalMultiplier();
        resourceManager.AddResource(ResourceType.Revenue, clickAmount);
        resourceManager.AddResource(ResourceType.Reputation, 1f);
        statusMessageText.text = "Status: Developed a game project.";
        SaveGameState();
        UpdateGeneratorUI();
        UpdateUpgradeUI();
    }

    public void BuyJuniorDeveloper()
    {
        string message;
        bool success = TryPurchaseGenerator(juniorDeveloperGenerator, out message);
        statusMessageText.text = "Status: " + message;

        if (success)
        {
            UpdateRevenuePerSecond();
            SaveGameState();
            UpdateGeneratorUI();
            UpdateUpgradeUI();
        }
    }

    public void BuySeniorDeveloper()
    {
        string message;
        bool success = TryPurchaseGenerator(seniorDeveloperGenerator, out message);
        statusMessageText.text = "Status: " + message;

        if (success)
        {
            UpdateRevenuePerSecond();
            SaveGameState();
            UpdateGeneratorUI();
            UpdateUpgradeUI();
        }
    }

    public void BuyReleasedGame()
    {
        string message;
        bool success = TryPurchaseGenerator(releasedGameGenerator, out message);
        statusMessageText.text = "Status: " + message;

        if (success)
        {
            resourceManager.AddResource(ResourceType.Reputation, 5f);
            UpdateRevenuePerSecond();
            SaveGameState();
            UpdateGeneratorUI();
            UpdateUpgradeUI();
        }
    }

    private bool TryPurchaseGenerator(Generator generator, out string message)
    {
        float cost = generator.GetCurrentCost();

        if (resourceManager.SpendResource(ResourceType.Revenue, cost))
        {
            generator.ownedCount++;
            message = "Purchased " + generator.generatorName;
            return true;
        }

        message = "Not enough Revenue for " + generator.generatorName;
        return false;
    }

    public void BuyBetterEngine() { BuyUpgrade("Better Engine"); }
    public void BuyAssetLibrary() { BuyUpgrade("Asset Library"); }
    public void BuyBuildOptimization() { BuyUpgrade("Build Optimization"); }

    private void BuyUpgrade(string upgradeName)
    {
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].upgradeName != upgradeName)
                continue;

            if (upgrades[i].state != UpgradeState.Available || upgrades[i].IsFullyPurchased())
            {
                statusMessageText.text = "Status: Upgrade is not available.";
                return;
            }

            if (resourceManager.SpendResource(ResourceType.Revenue, upgrades[i].cost))
            {
                upgrades[i].purchaseCount++;
                if (upgrades[i].IsFullyPurchased())
                    upgrades[i].state = UpgradeState.Purchased;

                ApplyUpgradeEffect(upgrades[i].effect);
                UpdateRevenuePerSecond();
                UpdateUpgradeAvailability();
                SaveGameState();
                statusMessageText.text = "Status: Purchased " + upgrades[i].upgradeName
                    + " (" + upgrades[i].purchaseCount + "/" + upgrades[i].maxPurchases + ")";
                UpdateGeneratorUI();
                UpdateUpgradeUI();
            }
            else
            {
                statusMessageText.text = "Status: Not enough Revenue for " + upgrades[i].upgradeName;
            }

            return;
        }
    }

    private void ApplyUpgradeEffect(UpgradeEffect effect)
    {
        if (effect.targetResourceType == ResourceType.RevenuePerSecond)
            globalRevenueMultiplier *= effect.multiplier;
    }

    // ─── App Lifecycle ───
    private void OnApplicationQuit()
    {
        SaveGameState();

        float sessionPlaytime = Time.time - sessionStartTime;
        string line = "Session Playtime: " + sessionPlaytime.ToString("F2") + " seconds - " + DateTime.Now;

        File.AppendAllText(playtimeFilePath, line + Environment.NewLine);
    }
}
