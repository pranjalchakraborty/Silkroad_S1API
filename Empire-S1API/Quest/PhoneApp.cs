using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using S1API.UI;
using S1API.Console;
using S1API.GameTime;
using Empire;
using System.Linq;
using MelonLoader.Utils;
using System.IO;
using S1API.Internal.Utils;
using Object = UnityEngine.Object;
using S1API.Money;

namespace Empire
{

    public class MyApp : S1API.PhoneApp.PhoneApp
    {
        public static BlackmarketBuyer saveBuyer { get; set; }
        protected override string AppName => "Empire";
        protected override string AppTitle => "Empire";
        protected override string IconLabel => "Empire";
        protected override string IconFileName => Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon.png");

        private List<QuestData> quests;
        private RectTransform questListContainer;
        private RectTransform buyerListContainer; // <-- Add this line
        private Text
            questTitle,
            questTask,
            questReward,
            deliveryStatus,
            acceptLabel,
            cancelLabel,
            refreshLabel,
            manageLabel,
            relationsLabel,
            productLabel,
            shippingLabel;
        private Button
            acceptButton,
            cancelButton,
            refreshButton,
            manageButton,
            relationsButton,
            productButton,
            shippingButton;
        private Text statusText;
        private Text managementTabLabel; // <<--- new field for the management tab label
        public static int Index;

        private BlackmarketBuyer selectedBuyer;
        private GameObject managementDetailPanel;

        //Bypass method to set quest image dynamically from dealer icon - not used - maybe TODO maybe NOT
        public static string QuestImage;
        protected override void OnCreated()
        {
            base.OnCreated();
            MelonLogger.Msg("[EmpireApp] OnCreated called");
        }

        private void InitializeDealers()
        {
            try
            {
                JSONDeserializer.Initialize();
                MelonLogger.Msg("✅ Dealers initialized");
                Contacts.Update();
                MelonLogger.Msg($"Contacts.Buyers Count: {Contacts.Buyers.Count}");
                TimeManager.OnDayPass += RefreshQuestList;
                MelonLogger.Msg("✅ TimeManager.OnDayPass event subscribed");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize dealers: {ex}");
            }
        }
        protected override void OnCreatedUI(GameObject container)
        {

            var bg = UIFactory.Panel("MainBG", container.transform, Color.black, fullAnchor: true);

            // Set the top bar title to "Deals" for the default quest screen.
            UIFactory.TopBar(name: "TopBar",
                parent: bg.transform,
                title: "Deals",
                topbarSize: 0.82f,
                paddingLeft: 75,
                paddingRight: 75,
                paddingTop: 0,
                paddingBottom: 35);

            // Status text below top bar
            statusText = UIFactory.Text("Status", "", bg.transform, 14);
            statusText.rectTransform.anchorMin = new Vector2(0.7f, 0.85f);
            statusText.rectTransform.anchorMax = new Vector2(0.98f, 0.9f);
            statusText.alignment = TextAnchor.MiddleRight;

            var leftPanel = UIFactory.Panel("QuestListPanel", bg.transform, new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0.02f, 0.05f), new Vector2(0.49f, 0.82f));
            questListContainer = UIFactory.ScrollableVerticalList("QuestListScroll", leftPanel.transform, out _);
            UIFactory.FitContentHeight(questListContainer);

            var rightPanel = UIFactory.Panel("DetailPanel", bg.transform, new Color(0.12f, 0.12f, 0.12f),
                new Vector2(0.49f, 0f), new Vector2(0.98f, 0.82f));

            // Use vertical layout with padding and spacing like Tax & Wash
            UIFactory.VerticalLayoutOnGO(rightPanel, spacing: 14, padding: new RectOffset(24, 50, 15, 70));
            questTitle = UIFactory.Text("Title", "", rightPanel.transform, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            questTask = UIFactory.Text("Task", "", rightPanel.transform, 18, TextAnchor.MiddleLeft, FontStyle.Normal);
            questReward = UIFactory.Text("Reward", "", rightPanel.transform, 18, TextAnchor.MiddleLeft, FontStyle.Normal);
            deliveryStatus = UIFactory.Text("DeliveryStatus", "", rightPanel.transform, 16, TextAnchor.MiddleLeft, FontStyle.Italic);
            deliveryStatus.color = new Color(32, 0x82, 0xF6, 0xff);
            // Create a horizontal container for Refresh and Cancel
            var topButtonRow = UIFactory.Panel("TopButtonRow", rightPanel.transform, Color.clear);
            UIFactory.HorizontalLayoutOnGO(topButtonRow, spacing: 12);
            UIFactory.SetLayoutGroupPadding(topButtonRow.GetComponent<HorizontalLayoutGroup>(), 0, 0, 0, 0);

            // Accept Button (separate row)
            var acceptTuple = UIFactory.RoundedButtonWithLabel("AcceptBtn", "No quest selected", rightPanel.transform, new Color32(32, 0x82, 0xF6, 0xff), 460f, 60f, 22, Color.black);
            GameObject acceptGO = acceptTuple.Item1;
            Button acceptBtn = acceptTuple.Item2;
            Text acceptLbl = acceptTuple.Item3;
            acceptButton = acceptBtn;
            acceptLabel = acceptLbl;
            ButtonUtils.Disable(acceptBtn, acceptLabel, "No quest selected");

            // Cancel Button
            var cancelTuple = UIFactory.RoundedButtonWithLabel("CancelBtn", "Cancel current delivery", rightPanel.transform, new Color32(32, 0x82, 0xF6, 0xff), 460f, 60f, 22, Color.black);
            GameObject cancelGO = cancelTuple.Item1;
            Button cancelBtn = cancelTuple.Item2;
            Text cancelLbl = cancelTuple.Item3;
            cancelButton = cancelBtn;
            cancelLabel = cancelLbl;
            if (!QuestDelivery.QuestActive)
                ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");

            //Manage button
            var manageTuple = UIFactory.RoundedButtonWithLabel
                (
                    "ManageBtn",
                    "Manage",
                    bg.transform,
                    new Color(0.2f, 0.2f, 0.2f, 1f),
                    100, // Width 
                    40,  // Height
                    16,  // Font size
                    Color.white
                );
            GameObject manageGO = manageTuple.Item1;
            Button manageBtn = manageTuple.Item2;
            Text ManageLbl = manageTuple.Item3;
            manageButton = manageBtn;
            manageLabel = ManageLbl;

            ButtonUtils.AddListener(manageBtn, () => OpenManageUI(bg));

            // Set the position and size of the manage button
            var manageRect = manageGO.GetComponent<RectTransform>();
            manageRect.anchorMin = new Vector2(0.75f, 0.96f);
            manageRect.anchorMax = new Vector2(0.85f, 1f);
            manageRect.pivot = new Vector2(1f, 1f);
            manageRect.anchoredPosition = new Vector2(-10f, -10f);
            manageRect.sizeDelta = new Vector2(50, 25);

            // Refresh Button
            var refreshTuple = UIFactory.RoundedButtonWithLabel(
                "RefreshBtn",
                "Refresh orders",
                bg.transform,
                new Color(0.2f, 0.2f, 0.2f, 1f),
                300,
                90,
                22,
                Color.white
            );
            GameObject refreshGO = refreshTuple.Item1;
            Button refreshBtn = refreshTuple.Item2;
            Text refreshLbl = refreshTuple.Item3;
            refreshButton = refreshBtn;
            refreshLabel = refreshLbl;

            ButtonUtils.AddListener(refreshButton, () => RefreshButton());

            // Set the position and size of the refresh button
            var refreshRect = refreshGO.GetComponent<RectTransform>();
            refreshRect.anchorMin = new Vector2(0.9f, 0.96f);
            refreshRect.anchorMax = new Vector2(1f, 1f);
            refreshRect.pivot = new Vector2(1f, 1f);
            refreshRect.anchoredPosition = new Vector2(-10f, -10f);
            refreshRect.sizeDelta = new Vector2(50, 25);

            InitializeDealers();
            MelonCoroutines.Start(WaitForBuyerAndInitialize());
        }

        private void OpenManageUI(GameObject bg)
        {
            // Create main management modal panel
            var managementPanel = UIFactory.Panel("ManagementPanel", bg.transform, new Color(200 / 255f, 200 / 255f, 200 / 255f, 0.3f), fullAnchor: true);
            managementPanel.gameObject.SetActive(true);
            managementPanel.transform.SetAsLastSibling();

            // Create Top Bar panel (occupies top 15% of the modal)
            var topBar = UIFactory.Panel("ManageTopBar", managementPanel.transform, new Color(50 / 255f, 50 / 255f, 50 / 255f, 1f));
            var topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0, 0.85f); // Adjusted height for better visibility
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.offsetMin = Vector2.zero;
            topBarRect.offsetMax = Vector2.zero;
            UIFactory.HorizontalLayoutOnGO(topBar, spacing: 20, padLeft: 20, padRight: 20, padTop: 10, padBottom: 10, alignment: TextAnchor.MiddleLeft);

            // Add Reputation, Product, and Shipping buttons to the top bar
            var repTuple = UIFactory.RoundedButtonWithLabel("RepButton", "Reputation", topBar.transform, new Color(0.2f, 0.2f, 0.2f, 1f), 120, 40, 16, Color.white);
            GameObject repGO = repTuple.Item1;
            Button repBtn = repTuple.Item2;
            Text repLbl = repTuple.Item3;
            ButtonUtils.ClearListeners(repBtn);
            ButtonUtils.AddListener(repBtn, () => UpdateBuyerDetails("Reputation"));

            var prodTuple = UIFactory.RoundedButtonWithLabel("ProdButton", "Product", topBar.transform, new Color(0.2f, 0.2f, 0.2f, 1f), 120, 40, 16, Color.white);
            GameObject prodGO = prodTuple.Item1;
            Button prodBtn = prodTuple.Item2;
            Text prodLbl = prodTuple.Item3;
            ButtonUtils.ClearListeners(prodBtn);
            ButtonUtils.AddListener(prodBtn, () => UpdateBuyerDetails("Product"));

            var shipTuple = UIFactory.RoundedButtonWithLabel("ShipButton", "Shipping", topBar.transform, new Color(0.2f, 0.2f, 0.2f, 1f), 120, 40, 16, Color.white);
            GameObject shipGO = shipTuple.Item1;
            Button shipBtn = shipTuple.Item2;
            Text shipLbl = shipTuple.Item3;
            ButtonUtils.ClearListeners(shipBtn);
            ButtonUtils.AddListener(shipBtn, () => UpdateBuyerDetails("Shipping"));

            // Instead of creating a new spacerObj, add a flexible LayoutElement directly to topBar
            var spacer = topBar.AddComponent<LayoutElement>();
            spacer.flexibleWidth = 1;

            // Create active tab label
            managementTabLabel = UIFactory.Text("ManagementTabLabel", "Reputation", topBar.transform, 20, TextAnchor.MiddleCenter, FontStyle.Bold);

            // Create Close button at the far right in top bar
            var closeTuple = UIFactory.RoundedButtonWithLabel("CloseButton", "X", topBar.transform, new Color32(235, 53, 56, 255), 50, 40, 16, Color.white);
            GameObject closeGO = closeTuple.Item1;
            Button closeBtn = closeTuple.Item2;
            Text closeLbl = closeTuple.Item3;
            ButtonUtils.AddListener(closeBtn, () => Object.Destroy(managementPanel));
            closeGO.GetComponent<RectTransform>().SetAsLastSibling();

            // Create Content Panel (occupies remaining height reduced to 85%)
            var contentPanel = UIFactory.Panel("ManageContent", managementPanel.transform, new Color(0.12f, 0.12f, 0.12f, 1f));
            var contentRect = contentPanel.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 0.85f); // Reduced to 0.85f 
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // Left Panel: Buyer List (40% width)
            var leftPanel = UIFactory.Panel("BuyerListPanel", contentPanel.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
            var leftRect = leftPanel.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0, 0);
            leftRect.anchorMax = new Vector2(0.4f, 1);
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = Vector2.zero;
            buyerListContainer = UIFactory.ScrollableVerticalList("BuyerListScroll", leftPanel.transform, out _); // <-- Use buyerListContainer
            UIFactory.FitContentHeight(buyerListContainer);
            PopulateBuyerList(buyerListContainer.transform); // <-- Use buyerListContainer

            // Auto-select first buyer if any exists (to initialize selectedBuyer)
            if (buyerListContainer.childCount > 0)
            {
                var firstButton = buyerListContainer.GetChild(0).GetComponent<Button>();
                firstButton?.onClick.Invoke();
            }

            // Right Panel: Detail Display (60% width)
            var rightPanel = UIFactory.Panel("DetailPanel", contentPanel.transform, new Color(0.12f, 0.12f, 0.12f, 1f));
            var rightRect = rightPanel.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.4f, 0);
            rightRect.anchorMax = new Vector2(1, 0.85f);
            rightRect.offsetMin = Vector2.zero;
            rightRect.offsetMax = Vector2.zero;
            UIFactory.VerticalLayoutOnGO(rightPanel, spacing: 14, padding: new RectOffset(24, 50, 15, 70));
            managementDetailPanel = rightPanel; // store detail panel for updates
        }

        private void AddManagementButtons(Transform parent)
        {
            // Create a horizontal container for the three buttons.
            var buttonContainer = UIFactory.Panel("MgmtButtons", parent, Color.clear);
            UIFactory.HorizontalLayoutOnGO(buttonContainer, spacing: 10);
            // Create Reputation button:
            var repTuple = UIFactory.RoundedButtonWithLabel("RepButton", "Reputation", buttonContainer.transform, new Color(0.2f, 0.2f, 0.2f, 1f), 100, 40, 16, Color.white);
            GameObject repGO = repTuple.Item1;
            Button repBtn = repTuple.Item2;
            Text repLbl = repTuple.Item3;
            ButtonUtils.ClearListeners(repBtn);
            ButtonUtils.AddListener(repBtn, () => UpdateBuyerDetails("Reputation"));
            // Create Product button:
            var prodTuple = UIFactory.RoundedButtonWithLabel("ProdButton", "Product", buttonContainer.transform, new Color(0.2f, 0.2f, 0.2f, 1f), 100, 40, 16, Color.white);
            GameObject prodGO = prodTuple.Item1;
            Button prodBtn = prodTuple.Item2;
            Text prodLbl = prodTuple.Item3;
            ButtonUtils.ClearListeners(prodBtn);
            ButtonUtils.AddListener(prodBtn, () => UpdateBuyerDetails("Product"));
            // Create Shipping button:
            var shipTuple = UIFactory.RoundedButtonWithLabel("ShipButton", "Shipping", buttonContainer.transform, new Color(0.2f, 0.2f, 0.2f, 1f), 100, 40, 16, Color.white);
            GameObject shipGO = shipTuple.Item1;
            Button shipBtn = shipTuple.Item2;
            Text shipLbl = shipTuple.Item3;
            ButtonUtils.ClearListeners(shipBtn);
            ButtonUtils.AddListener(shipBtn, () => UpdateBuyerDetails("Shipping"));
        }

        private void UpdateBuyerDetails(string tab)
        {
            // NEW: Guard against null managementDetailPanel.
            if (managementDetailPanel == null)
            {
                MelonLogger.Warning("[MyApp] UpdateBuyerDetails called but managementDetailPanel is null.");
                return;
            }
            // Clear detail panel
            for (int i = managementDetailPanel.transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(managementDetailPanel.transform.GetChild(i).gameObject);
            }

            string content = "";
            if (tab == "Reputation")
            {
                var imagePath = selectedBuyer.DealerImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png");
                UIFactory.SetIcon(ImageUtils.LoadImage(imagePath), managementDetailPanel.transform);
                var icon = managementDetailPanel.transform.GetComponentInChildren<Image>();
                if (icon != null)
                    icon.GetComponent<RectTransform>().sizeDelta = new Vector2(128, 128);
                content = $"<b>Reputation:</b> {selectedBuyer._DealerData.Reputation}";
                // NEW: Append stylized Deal Days under reputation.
                if (selectedBuyer.DealDays != null && selectedBuyer.DealDays.Count > 0)
                {
                    string daysStr = string.Join(", ", selectedBuyer.DealDays);
                    content += $"\n\n<b><color=#FFA500>Deal Days:</color></b> <color=#FFFFFF>{daysStr}</color>";
                    MelonLogger.Msg($"[MyApp] Displaying Deal Days for {selectedBuyer.DealerName}: {daysStr}");
                }
                var pendingBuyers = Contacts.Buyers.Values
                    .Where(b => !b.IsInitialized &&
                                b.UnlockRequirements != null &&
                                b.UnlockRequirements.Any(r => r.Name == selectedBuyer.DealerName && r.MinRep > selectedBuyer._DealerData.Reputation))
                    .ToList();
                if (pendingBuyers.Count > 0)
                {
                    content += "\n\n<b>Pending Unlocks:</b>\n";
                    foreach (var buyer in pendingBuyers)
                    {
                        var req = buyer.UnlockRequirements.FirstOrDefault(r => r.Name == selectedBuyer.DealerName && r.MinRep > selectedBuyer._DealerData.Reputation);
                        content += $"• {buyer.DealerName}: Requires Rep {req?.MinRep}\n";
                    }
                }
                //MelonLogger.Msg($"[MyApp] Showing Reputation: {selectedBuyer._DealerData.Reputation}");
                UIFactory.Text("DetailText", content, managementDetailPanel.transform, 18);
            }
            else if (tab == "Product")
            {
                content = selectedBuyer.GetDrugUnlockInfo();
                //MelonLogger.Msg($"[MyApp] Displaying Product info from GetDrugUnlockInfo():\n{content}");
                UIFactory.Text("ProductDetailText", content, managementDetailPanel.transform, 18);
            }
            else if (tab == "Shipping")
            {
                int currentTier = selectedBuyer._DealerData.ShippingTier;
                string currentShipping = "";
                string nextShipping = "";
                if (selectedBuyer.Shippings != null && currentTier < selectedBuyer.Shippings.Count)
                {
                    var currentShip = selectedBuyer.Shippings[currentTier];
                    currentShipping = $"<b><color=#FF6347>Current Tier ({currentTier})</color></b>\n" +
                                      $"   • <i>Name:</i> <color=#FFFFFF>{currentShip.Name}</color>\n" +
                                      $"   • <i>Cost:</i> <color=#00FFFF>{currentShip.Cost}</color>\n" +
                                      $"   • <i>Unlock Rep:</i> <color=#00FF00>{currentShip.UnlockRep}</color>\n" +
                                      $"   • <i>Amounts:</i> <color=#FFFF00>{currentShip.MinAmount} - {currentShip.MaxAmount}</color>\n" +
                                      $"   • <i>Deal Modifier:</i> <color=#FFA500>{string.Join(", ", currentShip.DealModifier)}</color>\n";
                }
                else
                {
                    currentShipping = "<b><color=#FF6347>Current shipping info not available.</color></b>";
                    MelonLogger.Warning("[MyApp] Current shipping tier index out of range.");
                }
                if (selectedBuyer.Shippings != null && currentTier + 1 < selectedBuyer.Shippings.Count)
                {
                    var nextShip = selectedBuyer.Shippings[currentTier + 1];
                    nextShipping = $"<b><color=#FF6347>Next Tier ({currentTier + 1})</color></b>\n" +
                                   $"   • <i>Name:</i> <color=#FFFFFF>{nextShip.Name}</color>\n" +
                                   $"   • <i>Cost:</i> <color=#00FFFF>{nextShip.Cost}</color>\n" +
                                   $"   • <i>Unlock Rep:</i> <color=#00FF00>{nextShip.UnlockRep}</color>\n" +
                                   $"   • <i>Amounts:</i> <color=#FFFF00>{nextShip.MinAmount} - {nextShip.MaxAmount}</color>\n" +
                                   $"   • <i>Deal Modifier:</i> <color=#FFA500>{string.Join(", ", nextShip.DealModifier)}</color>\n";
                }
                else
                {
                    nextShipping = "<b><color=#FF6347>Next Tier:</color></b> <color=#FFFFFF>Maximum tier unlocked.</color>";
                    MelonLogger.Msg("[MyApp] No next shipping tier available; maximum tier reached.");
                }
                content = currentShipping + "\n" + nextShipping;
                //MelonLogger.Msg($"[MyApp] Displaying Shipping info:\n{content}");
                UIFactory.Text("ShippingDetailText", content, managementDetailPanel.transform, 18);
                // Existing shipping upgrade code remains unchanged.
                if (selectedBuyer.Shippings != null && currentTier + 1 < selectedBuyer.Shippings.Count)
                {
                    var upgradeTuple = UIFactory.RoundedButtonWithLabel(
                        "UpgradeShippingButton",
                        "<b><i>Upgrade Shipping</i></b>",
                        managementDetailPanel.transform,
                        new Color32(0, 123, 255, 255),
                        240, 70, 22, Color.white);
                    GameObject upgradeGO = upgradeTuple.Item1;
                    Button upgradeBtn = upgradeTuple.Item2;
                    Text upgradeLbl = upgradeTuple.Item3;
                    upgradeLbl.text = "<i><color=#FFFFFF>Upgrade Shipping</color></i>";
                    ButtonUtils.ClearListeners(upgradeBtn);
                    ButtonUtils.AddListener(upgradeBtn, () =>
                    {
                        MelonLogger.Msg("[MyApp] Upgrade Shipping button clicked.");
                        var nextTier = selectedBuyer.Shippings[currentTier + 1];
                        int cost = nextTier.Cost;
                        int currentCash = (int)Money.GetCashBalance();
                        if (currentCash < cost)
                        {
                            MelonLogger.Warning($"[MyApp] Not enough cash for upgrade. Required: {cost} Current: {currentCash}");
                            UIFactory.Text("UpgradeErrorText", "<color=#FF0000>Not enough cash (cost: " + cost + ").</color>", managementDetailPanel.transform, 18);
                            return;
                        }
                        if (selectedBuyer._DealerData.Reputation < nextTier.UnlockRep)
                        {
                            MelonLogger.Warning($"[MyApp] Not enough reputation. Required: {nextTier.UnlockRep} Current: {selectedBuyer._DealerData.Reputation}");
                            UIFactory.Text("UpgradeErrorText", "<color=#FF0000>Not enough reputation (required: " + nextTier.UnlockRep + ").</color>", managementDetailPanel.transform, 18);
                            return;
                        }
                        bool upgraded = selectedBuyer.UpgradeShipping();
                        if (upgraded)
                        {
                            ConsoleHelper.RunCashCommand(-cost);
                            MelonLogger.Msg($"[MyApp] Shipping upgraded to tier {selectedBuyer._DealerData.ShippingTier}. Cost: {cost}");
                            UpdateBuyerDetails("Shipping");
                        }
                        else
                        {
                            MelonLogger.Msg("[MyApp] UpgradeShipping() returned false.");
                            UIFactory.Text("UpgradeErrorText", "<color=#FF0000>Maximum tier reached.</color>", managementDetailPanel.transform, 18);
                        }
                    });
                }
            }
            else
            {
                content = "No content available.";
                MelonLogger.Msg("[MyApp] Unknown tab requested.");
                UIFactory.Text("DefaultDetailText", content, managementDetailPanel.transform, 18);
            }
            // Update management tab label if applicable.
            if (managementTabLabel != null)
                managementTabLabel.text = tab;
        }

        private System.Collections.IEnumerator WaitForBuyerAndInitialize()
        {
            float timeout = 5f;
            float waited = 0f;

            MelonLogger.Msg("PhoneApp-WaitForBuyerAndInitialize-Waiting for Contacts to be initialized...");
            while ((!Contacts.IsUnlocked) && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null; // Wait for the next frame
            }

            // Check if the timeout was reached
            if (!Contacts.IsUnlocked)
            {
                MelonLogger.Warning("⚠️ PhoneApp-Timeout reached. Contacts are still not unlocked.");
                yield break; // Exit the coroutine
            }

            MelonLogger.Msg($"✅ Contacts initialized: {Contacts.Buyers.Count} buyers found.");

            MelonLogger.Msg("Dealers and Buyers initialized successfully.");
            // Load quests after initialization
            LoadQuests();
        }

        //Balance - TODO
        private void RefreshButton()
        {
            if (Money.GetCashBalance() < 420)
            {
                deliveryStatus.text = "You need 420 cash to refresh the list.";
                return;
            }
            //Not needed as LoadQuests() already clears the list AFTER loading the quests
            //RefreshQuestList();
            LoadQuests();
            ConsoleHelper.RunCashCommand(-420);
        }

        public static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                MelonLogger.Warning("[UIFactory] ClearChildren called with null parent.");
                return;
            }

            try
            {
                int count = parent.childCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    var child = parent.GetChild(i);
                    if (child != null)
                        Object.Destroy(child.gameObject);
                }

                MelonLogger.Msg($"[UIFactory] Cleared {count} children from: {parent.name}");
            }
            catch (System.Exception e)
            {
                MelonLogger.Error($"[UIFactory] Exception during ClearChildren: {e.Message}");
            }
        }
        //Clear Child given a parent transform and an index
        public static void ClearChild(Transform parent, int index)
        {
            if (parent == null || index < 0 || index >= parent.childCount)
            {
                MelonLogger.Warning($"[UIFactory] ClearChild called with invalid parameters.{{index:{index}, childCount:{parent.childCount}}}");
                return;
            }

            try
            {
                var child = parent.GetChild(index);
                if (child != null)
                    Object.Destroy(child.gameObject);

                MelonLogger.Msg($"[UIFactory] Cleared child at index {index} from: {parent.name}");
            }
            catch (System.Exception e)
            {
                MelonLogger.Error($"[UIFactory] Exception during ClearChild: {e.Message}");
            }
        }
        private void LoadQuests()
        {
            quests = new List<QuestData>();
            //Extra Logging
            MelonLogger.Msg(Contacts.Buyers);
            foreach (var buyer in Contacts.Buyers.Values)
            {
                // New: Only process buyer if its dealDays contains current day.
                string currentDay = S1API.GameTime.TimeManager.CurrentDay.ToString();
                if (buyer.DealDays == null || !buyer.DealDays.Contains(currentDay))
                {
                    MelonLogger.Msg($"[MyApp] Skipping buyer: {buyer.DealerName} as current day {currentDay} is not in their dealDays.");
                    continue;
                }
                MelonLogger.Msg($"[MyApp] Processing buyer: {buyer.DealerName} for current day {currentDay}");
                // ...existing code...
                var dealerSaveData = BlackmarketBuyer.GetDealerSaveData(buyer.DealerName);
                if (dealerSaveData == null)
                {
                    MelonLogger.Warning($"⚠️ Dealer {buyer.DealerName} not found in Buyers dictionary.");
                    continue;
                }
                if (buyer.IsInitialized == false)
                {
                    MelonLogger.Warning($"⚠️ Dealer {buyer.DealerName} found in Buyers dictionary as not unlocked. Not progressing further");
                    continue;
                }
                var shipping = buyer.Shippings[dealerSaveData.ShippingTier];
                // Log dealer information
                MelonLogger.Msg($"✅ Processing dealer: {buyer.DealerName}");
                //MelonLogger.Msg($"   Unlocked Drugs: {string.Join(", ", dealerSaveData.UnlockedDrugs)}");
                MelonLogger.Msg($"   MinDeliveryAmount: {shipping.MinAmount}, MaxDeliveryAmount: {shipping.MaxAmount}");

                //drugTypes are unique dealerSaveData.UnlockedDrugs.Type 
                var drugTypes = dealerSaveData.UnlockedDrugs.Select(d => d.Type).Distinct().ToArray();
                // order drugTypes randomly
                drugTypes = drugTypes.OrderBy(_ => UnityEngine.Random.value).ToArray();
                // Iterate through unlocked drugs and generate a quest for each drugTypes in dealerSaveData
                foreach (var drugType in drugTypes)
                {
                    if (dealerSaveData.UnlockedDrugs.Any(d => d.Type == drugType))
                    {
                        GenerateQuest(buyer, dealerSaveData, drugType);
                        break; // Only 1 quest per dealer
                    }
                }
            }

            // Log the total number of quests loaded
            MelonLogger.Msg($"✅ Total quests loaded: {quests.Count}");

            // Refresh the UI to display the quests
            RefreshQuestList();
        }
        int RoundToHalfMSD(int value)
        {
            if (value == 0) return 0;

            // Count number of digits in the number
            int digits = (int)Math.Floor(Math.Log10(value)) + 1;

            // Calculate how many most significant digits to keep
            int keep = (digits + 1) / 2;

            // Determine rounding base (i.e., 10, 100, 1000, etc.)
            int roundFactor = (int)Math.Pow(10, digits - keep);

            // Round up to nearest multiple of roundFactor
            int rounded = ((value + roundFactor - 1) / roundFactor) * roundFactor;

            return rounded;
        }


        private void GenerateQuest(BlackmarketBuyer buyer, DealerSaveData dealerSaveData, string drugType)
        {
            var shipping = buyer.Shippings[dealerSaveData.ShippingTier];
            int minAmount = shipping.MinAmount;
            int maxAmount = shipping.MaxAmount;
            if (buyer.RepLogBase > 1)
            {
                double logResult = Math.Log((double)buyer._DealerData.Reputation, (double)buyer.RepLogBase);
                // Clamp logResult so that it is at worst 0 - and offset by 4 - UPDATABLE
                if (logResult < 5)
                    logResult = 0;
                else
                    logResult = logResult - 5;
                minAmount = (int)(minAmount * (1 + logResult));
                maxAmount = (int)(maxAmount * (1 + logResult));
            }
            //Setting order amount
            int steps = (maxAmount - minAmount) / shipping.StepAmount;
            int randomStep = RandomUtils.RangeInt(0, steps);
            int amount = minAmount + randomStep * shipping.StepAmount;

            var unlockedDrugs = dealerSaveData.UnlockedDrugs.Where(d => d.Type == drugType).ToList();


            if (unlockedDrugs.Count == 0)
            {
                MelonLogger.Warning($"⚠️ No unlocked drugs of type {drugType} found for dealer {buyer.DealerName}.");
                return;
            }
            var necessaryEffects = new List<string>();
            var necessaryEffectMult = new List<float>();
            var optionalEffects = new List<string>();
            var optionalEffectMult = new List<float>();
            var quality = "";
            var aggregateDollarMultMin = 0f;
            var aggregateDollarMultMax = 0f;
            var randomIndex = UnityEngine.Random.Range(0, buyer.Deals.Count);
            int dealTime = (int)(buyer.Deals[randomIndex][0] * shipping.DealModifier[0]);
            float dealTimesMult = (float)(buyer.Deals[randomIndex][1] * shipping.DealModifier[1]);
            //Get a random drug from the unlocked drugs
            var randomDrug = unlockedDrugs[RandomUtils.RangeInt(0, unlockedDrugs.Count)];
            //Store the last quality. Also store dollar and rep multiplier
            //var lastQuality = randomDrug.Qualities.LastOrDefault();
            var qualityMult = 0f;
            //choose a random quality from the randomDrug.Qualities list
            var randomQuality = randomDrug.Qualities[RandomUtils.RangeInt(0, randomDrug.Qualities.Count)];
            var qualityKey = randomQuality.Type.Trim();
            if (JSONDeserializer.QualitiesDollarMult.ContainsKey(qualityKey))
            {
                quality = qualityKey;
                qualityMult = randomQuality.DollarMult;
            }
            else
            {
                MelonLogger.Warning($"⚠️ No dollar multiplier found for quality {qualityKey}.");
            }
            aggregateDollarMultMin = 1 + qualityMult;
            aggregateDollarMultMax = 1 + qualityMult;

            var tempMult11 = 1f;//min

            var tempMult21 = 1f;//max


            //Iterate through randomDrug.Effects and check if the effect is necessary or optional. Also multiply aggregate dollar and rep multipliers with base dollar+sum of effects dollar mult. Same for rep.
            var randomNum1 = 0.3f;//$ Effect Mult Random - JSON - TODO
            foreach (var effect in randomDrug.Effects)
            {
                if (effect.Probability > 1f && effect.Probability <= 2f && UnityEngine.Random.Range(0f, 1f) < effect.Probability - 1f)
                {
                    if (effect.Name != "Random")
                    {
                        // Standardize effect name
                        var effectKey = effect.Name.Trim().ToLowerInvariant();
                        necessaryEffects.Add(effectKey);
                        float effectDollarMult = effect.DollarMult;
                        if (JSONDeserializer.EffectsDollarMult.ContainsKey(effectKey))
                        {
                            effectDollarMult += JSONDeserializer.EffectsDollarMult[effectKey];
                        }
                        else
                        {
                            MelonLogger.Warning($"⚠️ No dollar multiplier found for effect {effectKey}.");
                        }
                        necessaryEffectMult.Add(effectDollarMult * randomNum1);
                        tempMult11 += effectDollarMult * randomNum1;
                        tempMult21 += effectDollarMult * randomNum1;
                    }
                    else
                    {
                        var randomEffect = Contacts.dealerData.EffectsName
                            .Where(name => !necessaryEffects.Contains(name.Trim().ToLowerInvariant()) && !optionalEffects.Contains(name.Trim().ToLowerInvariant()))
                            .OrderBy(_ => UnityEngine.Random.value)
                            .FirstOrDefault();
                        if (randomEffect != null)
                        {
                            var formattedRandomEffect = randomEffect.Trim().ToLowerInvariant();
                            necessaryEffects.Add(formattedRandomEffect);
                            if (!JSONDeserializer.EffectsDollarMult.ContainsKey(formattedRandomEffect))
                            {
                                MelonLogger.Warning($"⚠️ No dollar multiplier found for effect {formattedRandomEffect}.");
                            }
                            else
                            {
                                var EffectDollarMult = effect.DollarMult;
                                if (JSONDeserializer.QualitiesDollarMult.ContainsKey(formattedRandomEffect))
                                {
                                    EffectDollarMult += JSONDeserializer.QualitiesDollarMult[formattedRandomEffect];
                                }
                                else
                                {
                                    MelonLogger.Warning($"⚠️ No dollar multiplier found for effect {formattedRandomEffect}.");
                                }
                                necessaryEffectMult.Add(EffectDollarMult * randomNum1);
                                tempMult11 += EffectDollarMult * randomNum1;
                                tempMult21 += EffectDollarMult * randomNum1;
                            }
                        }
                    }
                }
                else if (effect.Probability > 0f && effect.Probability <= 1f && UnityEngine.Random.Range(0f, 1f) < effect.Probability)
                {
                    if (effect.Name != "Random")
                    {
                        var effectKey = effect.Name.Trim().ToLowerInvariant();
                        optionalEffects.Add(effectKey);
                        float effectDollarMult = effect.DollarMult;
                        if (JSONDeserializer.EffectsDollarMult.ContainsKey(effectKey))
                        {
                            effectDollarMult += JSONDeserializer.EffectsDollarMult[effectKey];
                        }
                        else
                        {
                            MelonLogger.Warning($"⚠️ No dollar multiplier found for effect {effectKey}.");
                        }
                        optionalEffectMult.Add(effectDollarMult * randomNum1);
                        tempMult21 += effectDollarMult * randomNum1;
                    }
                    else
                    {
                        var randomEffect = Contacts.dealerData.EffectsName
                            .Where(name => !necessaryEffects.Contains(name.Trim().ToLowerInvariant()) && !optionalEffects.Contains(name.Trim().ToLowerInvariant()))
                            .OrderBy(_ => UnityEngine.Random.value)
                            .FirstOrDefault();
                        if (randomEffect != null)
                        {
                            var formattedRandomEffect = randomEffect.Trim().ToLowerInvariant();
                            optionalEffects.Add(formattedRandomEffect);
                            if (JSONDeserializer.EffectsDollarMult.ContainsKey(formattedRandomEffect))
                            {
                                var EffectDollarMult = effect.DollarMult;
                                if (JSONDeserializer.QualitiesDollarMult.ContainsKey(formattedRandomEffect))
                                {
                                    EffectDollarMult += JSONDeserializer.QualitiesDollarMult[formattedRandomEffect];
                                }
                                else
                                {
                                    MelonLogger.Warning($"⚠️ No dollar multiplier found for effect {formattedRandomEffect}.");
                                }
                                optionalEffectMult.Add(EffectDollarMult * randomNum1);
                                tempMult21 += EffectDollarMult * randomNum1;
                            }
                            else
                            {
                                MelonLogger.Warning($"⚠️ No dollar multiplier found for effect {formattedRandomEffect}.");
                            }
                        }
                    }
                }
            }
            MelonLogger.Msg($"aggregateDollarMultMin: {aggregateDollarMultMin} tempMult11: {tempMult11} tempMult21: {tempMult21} dealTimesMult: {dealTimesMult}");
            aggregateDollarMultMin *= tempMult11;
            aggregateDollarMultMax *= tempMult21;

            //remove from randomDrug.Effects the optional effects that are not in the list of optional effects and have a probability < 1f
            //randomDrug.Effects.RemoveAll(effect => !optionalEffects.Contains(effect.Name) && effect.Probability < 1f);

            string effectDesc = "";
            if (necessaryEffects.Count > 0)
                effectDesc += $"Required: {string.Join(", ", necessaryEffects)}";
            if (optionalEffects.Count > 0)
                effectDesc += (effectDesc.Length > 0 ? "; " : "") + $"Optional: {string.Join(", ", optionalEffects)}";

            var TimeLimit = 3;
            var TimeLimitMult = 1f;
            var Penalties = new List<int> { 0, 0 };
            //Roll a random index for buyer.DealTimes

            //roll a random number to scale various values
            // JSON - TODO
            var randomNum2 = UnityEngine.Random.Range(0.5f, 1.5f);//Rep Random
            var randomNum3 = UnityEngine.Random.Range(0.5f, 1.5f);//XP Random
            var randomNum4 = UnityEngine.Random.Range(0.5f, 1.5f);//$ Base Random
            MelonLogger.Msg($"RandomNum1: {randomNum1}, RandomNum2: {randomNum2}, RandomNum3: {randomNum3}, RandomNum4: {randomNum4}");
            //If dealTimesMult>1 subtract 1 else multiply by randomNum1
            /*if (dealTimesMult > 1)
            {
                dealTimesMult = Math.Min(dealTimesMult - 1, dealTimesMult * randomNum1);
            }
            else
            {
                dealTimesMult *= randomNum1;
            }*/
            aggregateDollarMultMin *= dealTimesMult;
            aggregateDollarMultMax *= dealTimesMult;
            var quest = new QuestData
            {
                Title = $"{buyer.DealerName} wants {drugType} delivered.",
                Task = $"Deliver {amount}x {quality} {drugType}",
                ProductID = drugType,
                AmountRequired = (uint)amount,
                TargetObjectName = buyer.DealerName,
                DealerName = buyer.DealerName,
                QuestImage = Path.Combine(MelonEnvironment.ModsDirectory, "Empire", buyer.DealerImage ?? "EmpireIcon_quest.png"),
                BaseDollar = RoundToHalfMSD((int)(randomDrug.BaseDollar * randomNum4)),
                BaseRep = RoundToHalfMSD((int)(randomDrug.BaseRep * randomNum2)),
                BaseXp = RoundToHalfMSD((int)(randomDrug.BaseXp * randomNum3)),
                RepMult = randomDrug.RepMult * randomNum2,
                XpMult = randomDrug.XpMult * randomNum3,
                DollarMultiplierMin = (float)Math.Round(aggregateDollarMultMin, 2),
                DollarMultiplierMax = (float)Math.Round(aggregateDollarMultMax, 2),

                DealTime = dealTime,
                DealTimeMult = dealTimesMult * randomNum4,
                Penalties = new List<int> { RoundToHalfMSD((int)(buyer.Deals[randomIndex][2] * shipping.DealModifier[2] * randomNum1)), RoundToHalfMSD((int)(buyer.Deals[randomIndex][3] * shipping.DealModifier[3] * randomNum2)) },

                Quality = quality,
                QualityMult = qualityMult,
                NecessaryEffects = necessaryEffects,
                NecessaryEffectMult = necessaryEffectMult,
                OptionalEffects = optionalEffects,
                OptionalEffectMult = optionalEffectMult,
                Index = Index++
            };

            quests.Add(quest);

            //MelonLogger.Msg($"✅ Quest generated:");
            //MelonLogger.Msg($"   Title: {quest.Title}");
            //MelonLogger.Msg($"   Task: {quest.Task}");
            //MelonLogger.Msg($"   Amount Required: {quest.AmountRequired}");
            //MelonLogger.Msg($"   Required Drug: {quest.RequiredDrug}");
        }
        private void CancelCurrentQuest(QuestData quest)
        {
            var active = QuestDelivery.Active;
            if (active == null)
            {
                MelonLogger.Warning("❌ No active QuestDelivery found to cancel.");
                deliveryStatus.text = "❌ No active delivery to cancel.";
                return;
            }

            MelonLogger.Msg($"Active quest : {active.Data.ProductID} ");
            try
            {
                active.ForceCancel();
                deliveryStatus.text = "🚫 Delivery canceled.";
                ButtonUtils.Disable(cancelButton, cancelLabel, "Canceled");
                ButtonUtils.Enable(acceptButton, acceptLabel, "Accept Delivery");

                // Remove quest from the UI and underlying list, then refresh the list.
                if (questListContainer != null)
                {
                    //ClearChild(questListContainer, quest.Index);
                    //quests.Remove(quest);
                    RefreshQuestList();
                }
                else
                {
                    MelonLogger.Error("❌ questListContainer is null - Cancel Quest.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"❌ CancelCurrentQuest() exception: {ex}");
                deliveryStatus.text = "❌ Cancel failed.";
            }
        }
        private void RefreshQuestList()
        {
            ClearChildren(questListContainer);
            // Reset index if needed
            Index = 0;
            foreach (var quest in quests)
            {
                if (quest == null) continue;

                // Create the quest row and assign the quest entry UI elements
                var row = UIFactory.CreateQuestRow(quest.Title, questListContainer, out var iconPanel, out var textPanel);
                // Set the quest's index based on the actual sibling index in questListContainer
                quest.Index = row.transform.GetSiblingIndex();

                UIFactory.SetIcon(
                    ImageUtils.LoadImage(quest.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png")),
                    iconPanel.transform
                );
                // Force quest icon to display at 128x128
                var questIcon = iconPanel.transform.GetComponentInChildren<Image>();
                if (questIcon != null)
                    questIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(128, 128);
                ButtonUtils.AddListener(row.GetComponent<Button>(), () => OnSelectQuest(quest));
                UIFactory.CreateTextBlock(textPanel.transform, quest.Title, quest.Task,
                    QuestDelivery.CompletedQuestKeys?.Contains($"{quest.ProductID}_{quest.AmountRequired}") == true);
            }
        }
        private void PopulateBuyerList(Transform container)
        {
            ClearChildren(container);
            foreach (var buyer in Contacts.Buyers.Values)
            {
                if (!buyer.IsInitialized)
                    continue;
                var row = UIFactory.CreateQuestRow(buyer.DealerName, container, out var iconPanel, out var textPanel);
                // Set dealer icon (using a default if missing)
                UIFactory.SetIcon(
                    ImageUtils.LoadImage(Path.Combine(MelonEnvironment.ModsDirectory, "Empire", buyer.DealerImage ?? "EmpireIcon_quest.png")),
                    iconPanel.transform
                );
                ButtonUtils.AddListener(row.GetComponent<Button>(), () =>
                {
                    selectedBuyer = buyer;
                    // Use current tab in top bar (or default to "Reputation")
                    UpdateBuyerDetails(managementTabLabel != null ? managementTabLabel.text : "Reputation");
                });
                UIFactory.CreateTextBlock(textPanel.transform, buyer.DealerName, "", false);
            }
        }
        private void OnSelectQuest(QuestData quest)
        {
            var Buyer = Contacts.GetBuyer(quest.DealerName);
            var dialogue = Buyer.SendCustomMessage("DealStart", quest.ProductID, (int)quest.AmountRequired, quest.Quality, quest.NecessaryEffects, quest.OptionalEffects, true);
            questTitle.text = quest.Title;
            questTask.text = $"{dialogue}";
            questReward.text =
    $"<b><color=#FFD700>Rewards:</color></b> <color=#00FF00>${quest.BaseDollar} / {quest.BaseDollar / quest.AmountRequired} per piece</color> + <i>Price x</i> (<color=#00FFFF>{quest.DollarMultiplierMin}</color> - <color=#00FFFF>{quest.DollarMultiplierMax}</color>)\n" +
    $"<b><color=#FFD700>Reputation:</color></b> <color=#00FF00>{quest.BaseRep}</color> + Rewards x <color=#00FFFF>{Math.Round(quest.RepMult, 4)}</color>\n" +
    $"<b><color=#FFD700>XP:</color></b> <color=#00FF00>{quest.BaseXp}</color> + Rewards x <color=#00FFFF>{Math.Round(quest.XpMult, 4)}</color>\n\n" +
    $"<b><color=#FF6347>Deal Expiry:</color></b> <color=#FFA500>{quest.DealTime} min</color>\n" +
    $"<b><color=#FF6347>Failure Penalties:</color></b> <color=#FF0000>${quest.Penalties[0]}</color> + <color=#FF4500>{quest.Penalties[1]} Rep</color>";

            deliveryStatus.text = "";
            if (!QuestDelivery.QuestActive)
            {
                ButtonUtils.Enable(acceptButton, acceptLabel, "Accept Delivery");
                ButtonUtils.ClearListeners(acceptButton);
                ButtonUtils.AddListener(acceptButton, () => AcceptQuest(quest));
            }

            if (QuestDelivery.QuestActive)
            {
                ButtonUtils.Enable(acceptButton, acceptLabel, "In progress");
                ButtonUtils.ClearListeners(acceptButton);
                ButtonUtils.AddListener(acceptButton, () => AcceptQuest(quest));
                ButtonUtils.Enable(cancelButton, cancelLabel, "Cancel current delivery");
                ButtonUtils.ClearListeners(cancelButton);
                ButtonUtils.AddListener(cancelButton, () => CancelCurrentQuest(quest));
            }

            //ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");
            ButtonUtils.ClearListeners(cancelButton);
            ButtonUtils.AddListener(cancelButton, () => CancelCurrentQuest(quest));
            ButtonUtils.Enable(refreshButton, refreshLabel, "Refresh orders");
            ButtonUtils.ClearListeners(refreshButton);
            ButtonUtils.AddListener(refreshButton, () => RefreshButton());

        }
        public void OnQuestComplete(QuestData quest)
        {
            //Remove Listeners from cancelBtn and make it say No quest active
            ButtonUtils.ClearListeners(cancelButton);
            ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");
        }

        private void AcceptQuest(QuestData quest)
        {
            if (QuestDelivery.QuestActive)
            {
                deliveryStatus.text = "⚠️ Finish your current job first!";
                ButtonUtils.Disable(acceptButton, acceptLabel, "In Progress");
                ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color32(32, 0x82, 0xF6, 0xff));
                return;
            }
            var Buyer = Contacts.GetBuyer(quest.DealerName);
            Buyer.SendCustomMessage("DealStart", quest.ProductID, (int)quest.AmountRequired, quest.Quality, quest.NecessaryEffects, quest.OptionalEffects);
            MelonLogger.Msg("✅ Deal started:");
            deliveryStatus.text = "📦 Delivery started!";
            ButtonUtils.Disable(acceptButton, acceptLabel, "In Progress");
            Buyer = Contacts.GetBuyer(quest.DealerName);
            var q = S1API.Quests.QuestManager.CreateQuest<QuestDelivery>();
            if (q is QuestDelivery delivery)
            {
                // Populate delivery data...
                delivery.Data.ProductID = quest.ProductID;
                delivery.Data.RequiredAmount = quest.AmountRequired;
                delivery.Data.DealerName = quest.DealerName;
                delivery.Data.QuestImage = quest.QuestImage;
                delivery.Data.Reward = quest.BaseDollar;
                delivery.Data.RepReward = quest.BaseRep;
                delivery.Data.XpReward = quest.BaseXp;
                delivery.Data.RepMult = quest.RepMult;
                delivery.Data.XpMult = quest.XpMult;
                delivery.Data.Task = quest.Task;
                delivery.Data.DealTime = quest.DealTime;
                delivery.Data.DealTimeMult = quest.DealTimeMult;
                delivery.Data.Penalties = quest.Penalties;
                delivery.Data.Quality = quest.Quality;
                delivery.Data.QualityMult = quest.QualityMult;
                delivery.Data.NecessaryEffects = quest.NecessaryEffects;
                delivery.Data.OptionalEffects = quest.OptionalEffects;
                delivery.Data.NecessaryEffectMult = quest.NecessaryEffectMult;
                delivery.Data.OptionalEffectMult = quest.OptionalEffectMult;
                QuestDelivery.Active = delivery;
                if (Buyer is BlackmarketBuyer buyer)
                    buyer.SendCustomMessage("Accept", quest.ProductID, (int)quest.AmountRequired, quest.Quality, quest.NecessaryEffects, quest.OptionalEffects);
            }
            else
            {
                MelonLogger.Error("❌ Failed to create QuestDelivery instance - Accept Quest.");
                return;
            }
            MelonLogger.Msg($"✅ Quest accepted: {quest.Title}");
            // Remove quest from list: remove quest then refresh the entire quest list container.
            if (questListContainer != null)
            {
                quests.Remove(quest);
                RefreshQuestList();
            }
            else
            {
                MelonLogger.Error("❌ questListContainer is null - Accept Quest.");
            }
            ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color32(32, 0x82, 0xF6, 0xff));
            acceptButton.interactable = false;
            ButtonUtils.Enable(cancelButton, cancelLabel, "Cancel Current Delivery");
        }
    }
}