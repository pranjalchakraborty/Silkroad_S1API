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
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize dealers: {ex}");
            }
        }
        protected override void OnCreatedUI(GameObject container)
        {

            var bg = UIFactory.Panel("MainBG", container.transform, Color.black, fullAnchor: true);

            UIFactory.TopBar(name: "TopBar",
                parent: bg.transform,
                title: "Empire",
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

            // Header
            //questTitle = UIFactory.Text("Title", "Select a quest", rightPanel.transform, 24, TextAnchor.MiddleLeft, FontStyle.Bold);

            // Styled task/reward rows (Label + Value style)
            //questTask = UIFactory.Text("Task", "Task: --", rightPanel.transform, 18, TextAnchor.MiddleLeft, FontStyle.Normal);
            //questReward = UIFactory.Text("Reward", "Reward: --", rightPanel.transform, 18, TextAnchor.MiddleLeft, FontStyle.Normal);
            //deliveryStatus = UIFactory.Text("Delivery", "", rightPanel.transform, 16);

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
            var (acceptGO, acceptBtn, acceptLbl) = UIFactory.RoundedButtonWithLabel("AcceptBtn", "No quest selected", rightPanel.transform, new Color32(32, 0x82, 0xF6, 0xff), 460f, 60f, 22, Color.black);

            acceptButton = acceptBtn;
            acceptLabel = acceptLbl;
            ButtonUtils.Disable(acceptBtn, acceptLabel, "No quest selected");

            // Cancel Button
            var (cancelGO, cancelBtn, cancelLbl) = UIFactory.RoundedButtonWithLabel("CancelBtn", "Cancel current delivery", rightPanel.transform, new Color32(32, 0x82, 0xF6, 0xff), 460f, 60f, 22, Color.black);
            cancelButton = cancelBtn;
            cancelLabel = cancelLbl;
            if (!QuestDelivery.QuestActive)
                ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");

            //Manage button
            var (manageGO, manageBtn, ManageLbl) =
                UIFactory.RoundedButtonWithLabel
                (
                    "ManageBtn",
                    "Manage",
                    bg.transform,
                    new Color(0.2f, 0.2f, 0.2f, 1f),
                    20,
                    20,
                    22,
                    Color.white
                );

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
            var (refreshGO, refreshBtn, refreshLbl) =
                UIFactory.RoundedButtonWithLabel(
                    "RefreshBtn",
                    "Refresh orders",
                    bg.transform,
                    new Color(0.2f, 0.2f, 0.2f, 1f),
                    300,
                    90,
                    22,
                    Color.white
                );
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
            var managementPanel = UIFactory.Panel("ManagementPanel", bg.transform, new Color(200, 200, 200, 0.3f), fullAnchor: true);
            managementPanel.gameObject.SetActive(true);
            managementPanel.transform.SetAsLastSibling();

            var (contentBackground, topBar, buttonRow) = SetupInitialPanel(managementPanel);

            // --- New: repurpose top bar buttons for buyer details ---
            // Change RelationsButton text to "Reputation" and ShippingButton text remains "Shipping"
            relationsLabel.text = "Reputation";
            shippingLabel.text = "Shipping";
            // Wire up new listeners for reputation and shipping
            ButtonUtils.ClearListeners(relationsButton);
            ButtonUtils.AddListener(relationsButton, () => UpdateBuyerDetails("Reputation"));
            ButtonUtils.ClearListeners(shippingButton);
            ButtonUtils.AddListener(shippingButton, () => UpdateBuyerDetails("Shipping"));
            // (Product button remains unchanged for now)
            // --- End new ---

            var detailsPanel = UIFactory.Panel("DetailsPanel", managementPanel.transform, new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0, 0), new Vector2(1, 0.82f));

            var leftPanel = UIFactory.Panel("BuyerListPanel", detailsPanel.transform, new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0.02f, 0.05f), new Vector2(0.49f, 0.82f));
            questListContainer = UIFactory.ScrollableVerticalList("BuyerListScroll", leftPanel.transform, out _);
            UIFactory.FitContentHeight(questListContainer);
            PopulateBuyerList(questListContainer); // populate left panel with buyers

            var rightPanel = UIFactory.Panel("DetailPanel", detailsPanel.transform, new Color(0.12f, 0.12f, 0.12f),
                new Vector2(0.49f, 0f), new Vector2(0.98f, 0.82f));
            UIFactory.VerticalLayoutOnGO(rightPanel, spacing: 14, padding: new RectOffset(24, 50, 15, 70));
            managementDetailPanel = rightPanel; // store reference for updates
        }

        private (GameObject contentBackground, GameObject topBar, GameObject buttonRow) SetupInitialPanel(GameObject managementPanel)
        {
            var contentBackground = UIFactory.Panel("ContentBackground", managementPanel.transform, new Color(0.2f, 0.2f, 0.2f, 1f));
            var contentRect = contentBackground.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.0f, 0.0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var topBar = UIFactory.Panel("TopBar", managementPanel.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
            var topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0f, 0.9f);
            topBarRect.anchorMax = new Vector2(1f, 1f);
            topBarRect.offsetMin = Vector2.zero;
            topBarRect.offsetMax = Vector2.zero;

            var buttonRow = UIFactory.ButtonRow("TopButtons", topBar.transform, spacing: 2);

            var buttonRowRect = buttonRow.GetComponent<RectTransform>();
            buttonRowRect.anchorMin = new Vector2(0, 1);
            buttonRowRect.anchorMax = new Vector2(0, 1);
            buttonRowRect.anchoredPosition = new Vector2(2, -33);
            buttonRowRect.sizeDelta = new Vector2(0, 60);

            AddButtonsToRow(buttonRow);

            var (closeGO, closeBtn, closeLbl) = UIFactory.RoundedButtonWithLabel(
                "CloseButton",
                "X",
                managementPanel.transform,
                new Color32(235, 53, 56, 255),
                50,
                20,
                12,
                Color.white
            );

            var closeRect = closeGO.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.98f, 0.98f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);
            closeRect.sizeDelta = new Vector2(25, 25);

            ButtonUtils.AddListener(closeBtn, () => Object.Destroy(managementPanel.gameObject));

            if (contentBackground == null || topBar == null || buttonRow == null)
            {
                MelonLogger.Error("Failed to create management panel components.");
                return (null, null, null);
            }

            return (contentBackground, topBar, buttonRow);
        }

        private void AddButtonsToRow(GameObject buttonRow)
        {
            var (relationsGO, relationsBtn, relationsLbl) = UIFactory.RoundedButtonWithLabel(
                "RelationsButton",
                "Relations",
                buttonRow.transform,
                new Color(0.2f, 0.2f, 0.2f, 1f),
                100,
                100,
                18,
                Color.white
            );
            RectTransformNavButton(relationsGO.GetComponent<RectTransform>());
            relationsButton = relationsBtn;
            relationsLabel = relationsLbl;
            ButtonUtils.ClearListeners(relationsButton);
            ButtonUtils.AddListener(relationsButton, () =>
            {
                MelonLogger.Msg("[MyApp] Relations button clicked.");
                UpdateBuyerDetails("Reputation");
            });

            var (productGO, productBtn, productLbl) = UIFactory.RoundedButtonWithLabel(
                "ProductButton",
                "Product",
                buttonRow.transform,
                new Color(0.2f, 0.2f, 0.2f, 1f),
                100,
                100,
                18,
                Color.white
            );
            productButton = productBtn;
            productLabel = productLbl;
            RectTransformNavButton(productGO.GetComponent<RectTransform>());
            ButtonUtils.ClearListeners(productButton);
            ButtonUtils.AddListener(productButton, () =>
            {
                MelonLogger.Msg("[MyApp] Product button clicked.");
                UpdateBuyerDetails("Product");
            });

            var (shippingGO, shippingBtn, shippingLbl) = UIFactory.RoundedButtonWithLabel(
                "ShippingButton",
                "Shipping",
                buttonRow.transform,
                new Color(0.2f, 0.2f, 0.2f, 1f),
                100,
                100,
                18,
                Color.white
            );
            shippingButton = shippingBtn;
            shippingLabel = shippingLbl;
            RectTransformNavButton(shippingGO.GetComponent<RectTransform>());
            ButtonUtils.ClearListeners(shippingButton);
            ButtonUtils.AddListener(shippingButton, () =>
            {
                MelonLogger.Msg("[MyApp] Shipping button clicked.");
                UpdateBuyerDetails("Shipping");
            });
        }

        private void RectTransformNavButton(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.05f, 0.5f);
            rect.anchorMax = new Vector2(0.05f, 0.5f);
            rect.anchoredPosition = new Vector2(10f, 10f);
            rect.sizeDelta = new Vector2(100, 60);
        }

        private void SetDetailsContent(Text description, string tab)
        {
            switch (tab)
            {
                case "Relations":
                    description.text = "Relations content goes here.";
                    break;
                case "Product":
                    description.text = "Product content goes here.";
                    break;
                case "Shipping":
                    description.text = "Shipping content goes here.";
                    break;
                default:
                    description.text = "No content available.";
                    break;
            }
        }

        private void PopulateBuyerList(Transform container)
        {
            ClearChildren(container);
            foreach (var buyer in Contacts.Buyers.Values)
            {
                if (!buyer.IsInitialized) continue;
                // Create a row using existing UIFactory row creation
                var row = UIFactory.CreateQuestRow(buyer.DealerName, container, out var iconPanel, out var textPanel);
                UIFactory.CreateTextBlock(textPanel.transform, buyer.DealerName, "", false);
                ButtonUtils.AddListener(row.GetComponent<Button>(), () => OnSelectBuyer(buyer));
            }
        }

        private void OnSelectBuyer(BlackmarketBuyer buyer)
        {
            selectedBuyer = buyer;
            UpdateBuyerDetails("Reputation"); // default to reputation view
        }

        private void UpdateBuyerDetails(string tab)
        {
            if (selectedBuyer == null)
            {
                MelonLogger.Warning("[MyApp] UpdateBuyerDetails called but no buyer is selected.");
                return;
            }

            // Clear detail panel using a for-loop to avoid invalid cast exceptions.
            for (int i = managementDetailPanel.transform.childCount - 1; i >= 0; i--)
            {
                var child = managementDetailPanel.transform.GetChild(i);
                Object.Destroy(child.gameObject);
            }

            string content = "";
            if (tab == "Reputation")
            {
                var imagePath = selectedBuyer.DealerImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png");
                UIFactory.SetIcon(ImageUtils.LoadImage(imagePath), managementDetailPanel.transform);
                // Force NPC image to display at 128x128
                var icon = managementDetailPanel.transform.GetComponentInChildren<Image>();
                if (icon != null)
                    icon.GetComponent<RectTransform>().sizeDelta = new Vector2(128, 128);
                
                // Beautified reputation text
                content = $"<b><color=#ADFF2F>Reputation:</color></b> <color=#FFFFFF>{selectedBuyer._DealerData.Reputation}</color>";
                // Updated pending unlocks: Only show if current rep is lower than the unlock requirement.
                var pendingBuyers = Contacts.Buyers.Values
                    .Where(b => !b.IsInitialized &&
                                b.UnlockRequirements != null &&
                                b.UnlockRequirements.Any(req => req.Name == selectedBuyer.DealerName 
                                                                && selectedBuyer._DealerData.Reputation < req.MinRep))
                    .ToList();
                if (pendingBuyers.Count > 0)
                {
                    content += "\n\n<b><color=#FF4500>Pending Unlocks:</color></b>\n";
                    foreach (var buyer in pendingBuyers)
                    {
                        var req = buyer.UnlockRequirements.FirstOrDefault(r => r.Name == selectedBuyer.DealerName);
                        content += $"• <color=#FFFFFF>{buyer.DealerName}</color>: Requires Rep <color=#00FFFF>{req?.MinRep}</color>\n";
                    }
                }
                MelonLogger.Msg($"[MyApp] Showing Reputation: {selectedBuyer._DealerData.Reputation}");
                UIFactory.Text("DetailText", content, managementDetailPanel.transform, 18);
            }
            else if (tab == "Product")
            {
                // NEW: Build product info with quality details and price multipliers.
                var dealerSaveData = BlackmarketBuyer.GetDealerSaveData(selectedBuyer.DealerName);
                if (dealerSaveData != null && dealerSaveData.UnlockedDrugs != null)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("<b><color=#FFD700>Available Products</color></b>");
                    foreach (var drug in dealerSaveData.UnlockedDrugs)
                    {
                        sb.AppendLine($"<color=#FFFFFF>{drug.Type}</color>:");
                        foreach (var qual in drug.Qualities)
                        {
                            sb.AppendLine($"\t• <i>{qual.Type}</i> - Price Multiplier: <color=#00FFFF>{qual.DollarMult}</color>");
                        }
                        sb.AppendLine();
                    }
                    content = sb.ToString();
                }
                else
                {
                    content = "<b><color=#FF6347>No product information available.</color></b>";
                }
                MelonLogger.Msg($"[MyApp] Displaying Product info:\n{content}");
                UIFactory.Text("ProductDetailText", content, managementDetailPanel.transform, 18);
            }
            else if (tab == "Shipping")
            {
                // Display current shipping tier info and next tier info if available.
                int currentTier = selectedBuyer._DealerData.ShippingTier;
                string currentShipping = "";
                string nextShipping = "";
                if (selectedBuyer.Shippings != null && currentTier < selectedBuyer.Shippings.Count)
                {
                    var currentShip = selectedBuyer.Shippings[currentTier];
                    currentShipping = $"<b>Current Tier ({currentTier}):</b>\n" +
                                    $"   • <i>Name:</i> {currentShip.Name}\n" +
                                    $"   • <i>Cost:</i> {currentShip.Cost}\n" +
                                    $"   • <i>Unlock Rep:</i> {currentShip.UnlockRep}\n" +
                                    $"   • <i>Amounts:</i> {currentShip.MinAmount} - {currentShip.MaxAmount}\n" +
                                    $"   • <i>Deal Modifier:</i> {string.Join(", ", currentShip.DealModifier)}\n";
                }
                else
                {
                    currentShipping = "Current shipping info not available.";
                    MelonLogger.Warning("[MyApp] Current shipping tier index out of range.");
                }

                if (selectedBuyer.Shippings != null && currentTier + 1 < selectedBuyer.Shippings.Count)
                {
                    var nextShip = selectedBuyer.Shippings[currentTier + 1];
                    nextShipping = $"<b>Next Tier ({currentTier + 1}):</b>\n" +
                                $"   • <i>Name:</i> {nextShip.Name}\n" +
                                $"   • <i>Cost:</i> {nextShip.Cost}\n" +
                                $"   • <i>Unlock Rep:</i> {nextShip.UnlockRep}\n" +
                                $"   • <i>Amounts:</i> {nextShip.MinAmount} - {nextShip.MaxAmount}\n" +
                                $"   • <i>Deal Modifier:</i> {string.Join(", ", nextShip.DealModifier)}\n";
                }
                else
                {
                    nextShipping = "<b>Next Tier:</b> Maximum tier unlocked.";
                    MelonLogger.Msg("[MyApp] No next shipping tier available; maximum tier reached.");
                }

                content = currentShipping + "\n" + nextShipping;
                MelonLogger.Msg($"[MyApp] Displaying Shipping info:\n{content}");
                UIFactory.Text("ShippingDetailText", content, managementDetailPanel.transform, 18);

                // Add an Upgrade Shipping button if a next tier exists
                if (selectedBuyer.Shippings != null && currentTier + 1 < selectedBuyer.Shippings.Count)
                {
                    var (upgradeGO, upgradeBtn, upgradeLbl) = UIFactory.RoundedButtonWithLabel(
                        "UpgradeShippingButton",
                        "Upgrade Shipping",
                        managementDetailPanel.transform,
                        new Color32(34, 130, 246, 255),
                        200,
                        50,
                        18,
                        Color.white
                    );
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
                            UIFactory.Text("UpgradeErrorText", $"Not enough cash for upgrade (cost: {cost}).", managementDetailPanel.transform, 18);
                            return;
                        }
                        bool upgraded = selectedBuyer.UpgradeShipping();
                        if (upgraded)
                        {
                            ConsoleHelper.RunCashCommand(-cost);
                            MelonLogger.Msg($"[MyApp] Shipping upgraded to tier {selectedBuyer._DealerData.ShippingTier}. Cost deducted: {cost}");
                            UpdateBuyerDetails("Shipping");
                        }
                        else
                        {
                            MelonLogger.Msg("[MyApp] UpgradeShipping() returned false. Maximum tier unlocked.");
                            UIFactory.Text("UpgradeErrorText", "Maximum shipping tier reached.", managementDetailPanel.transform, 18);
                        }
                    });
                }
            }
            else
            {
                content = "<b><i><color=#FF4500>No content available.</color></i></b>";
                MelonLogger.Msg("[MyApp] Unknown tab requested in UpdateBuyerDetails.");
                UIFactory.Text("DefaultDetailText", content, managementDetailPanel.transform, 18);
            }
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
            foreach (var buyer in Contacts.Buyers)
            {
                MelonLogger.Msg($"Buyer Key: {buyer.Key}, Dealer Name: {buyer.Value.DealerName}");
            }
            if (Contacts.Buyers == null)
            {
                MelonLogger.Error("❌ Contacts.Buyers is null. Ensure it is initialized before calling LoadQuests.");
                return;
            }
            if (Contacts.Buyers.Count == 0)
            {
                MelonLogger.Warning("⚠️ Contacts.Buyers.Count is empty. No buyers are available.");
                return;
            }


            foreach (var buyer in Contacts.Buyers.Values)
            {
                if (buyer == null)
                {
                    MelonLogger.Warning("⚠️ Buyer is null. Skipping...");
                    continue;
                }

                MelonLogger.Msg($"Processing buyer: {buyer.DealerName}");
                // Check if the dealer exists in the Buyers dictionary
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
                MelonLogger.Msg($"   Unlocked Drugs: {string.Join(", ", dealerSaveData.UnlockedDrugs)}");
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
            if (randomQuality != null)
            {
                quality = randomQuality.Type;
                qualityMult = randomQuality.DollarMult;
                aggregateDollarMultMin = 1 + randomQuality.DollarMult;
                aggregateDollarMultMax = aggregateDollarMultMin;

            }
            var tempMult11 = 1f;//min

            var tempMult21 = 1f;//max


            //Iterate through randomDrug.Effects and check if the effect is necessary or optional. Also multiply aggregate dollar and rep multipliers with base dollar+sum of effects dollar mult. Same for rep.
            var randomNum1 = UnityEngine.Random.Range(0.1f, 0.3f);//$ Effect Mult Random
            foreach (var effect in randomDrug.Effects)
            {
                //If the effect is necessary, add it to the necessaryEffects list and multiply the aggregate dollar and rep multipliers with the effect's dollar and rep multipliers
                //If the effect is optional, add it to the optionalEffects list and multiply the aggregate dollar and rep multipliers with the effect's dollar and rep multipliers
                //If the effect is not necessary or optional, skip it
                if (effect.Probability > 1f && effect.Probability <= 2f && UnityEngine.Random.Range(0f, 1f) < effect.Probability - 1f)
                {
                    if (effect.Name != "Random")
                    {
                        necessaryEffects.Add(effect.Name);
                        necessaryEffectMult.Add(effect.DollarMult * randomNum1);
                        tempMult11 += effect.DollarMult * randomNum1;
                        tempMult21 += effect.DollarMult * randomNum1;
                    }
                    else
                    {
                        // choose a random effect from Contacts.dealerData.EffectsName that is not in the necessaryEffects list or optionalEffects list
                        var randomEffect = Contacts.dealerData.EffectsName
                            .Where(name => !necessaryEffects.Contains(name) && !optionalEffects.Contains(name))
                            .OrderBy(_ => UnityEngine.Random.value)
                            .FirstOrDefault();
                        if (randomEffect != null)
                        {
                            necessaryEffects.Add(randomEffect);
                            // Random Hardcoded to Take from List - TODO - use based on take_from_list
                            if (!JSONDeserializer.EffectsDollarMult.ContainsKey(randomEffect))
                            {
                                MelonLogger.Warning($"⚠️ No dollar multiplier found for effect {randomEffect}.");
                            }
                            else
                            {
                                var EffectDollarMult = JSONDeserializer.EffectsDollarMult[randomEffect];
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
                        optionalEffects.Add(effect.Name);
                        optionalEffectMult.Add(effect.DollarMult * randomNum1);
                        tempMult21 += effect.DollarMult * randomNum1;
                    }
                    else
                    {
                        var randomEffect = Contacts.dealerData.EffectsName
                            .Where(name => !necessaryEffects.Contains(name) && !optionalEffects.Contains(name))
                            .OrderBy(_ => UnityEngine.Random.value)
                            .FirstOrDefault();
                        if (randomEffect != null)
                        {
                            optionalEffects.Add(randomEffect);
                            // Random Hardcoded to Take from List - TODO - use based on take_from_list
                            if (JSONDeserializer.EffectsDollarMult.ContainsKey(randomEffect))
                            {
                                var EffectDollarMult = JSONDeserializer.EffectsDollarMult[randomEffect];
                                optionalEffectMult.Add(EffectDollarMult * randomNum1);
                                tempMult21 += EffectDollarMult * randomNum1;
                            }
                            else
                            {
                                MelonLogger.Warning($"⚠️ No dollar multiplier found for effect {randomEffect}.");
                            }
                        }
                    }
                }
            }

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

            var randomNum2 = UnityEngine.Random.Range(0.5f, 1.5f);//Rep Random
            var randomNum3 = UnityEngine.Random.Range(0.5f, 1.5f);//XP Random
            var randomNum4 = UnityEngine.Random.Range(0.5f, 0.75f);//$ Base Random
            MelonLogger.Msg($"RandomNum1: {randomNum1}, RandomNum2: {randomNum2}, RandomNum3: {randomNum3}, RandomNum4: {randomNum4}");
            //If dealTimesMult>1 subtract 1 else multiply by randomNum1
            if (dealTimesMult > 1)
            {
                dealTimesMult = Math.Min(dealTimesMult - 1, dealTimesMult * randomNum1);
            }
            else
            {
                dealTimesMult *= randomNum1;
            }
            aggregateDollarMultMin *= dealTimesMult;
            aggregateDollarMultMax *= dealTimesMult;
            var quest = new QuestData
            {
                Title = $"{buyer.DealerName} wants {drugType} delivered.",
                Task = $"Deliver {amount}x {quality} {drugType}" + (effectDesc.Length > 0 ? $" with [{effectDesc}]" : ""),
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
                DealTimeMult = dealTimesMult,
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
    $"<b><color=#FF6347>Deal Expiry:</color></b> <color=#FFA500>{quest.DealTime}</color> Day(s)\n" +
    $"<b><color=#FF6347>Failure Penalties:</color></b> <color=#FF0000>${quest.Penalties[0]}</color> + <color=#FF4500>{quest.Penalties[1]} Rep</color>\n\n" +
    $"<b><color=#87CEEB>Current Reputation:</color></b> <color=#FFFFFF>{Buyer._DealerData.Reputation}</color>\n";

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
            MelonLogger.Msg($"✅ Deal started: ");

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
                {
                    buyer.SendCustomMessage("Accept", quest.ProductID, (int)quest.AmountRequired, quest.Quality, quest.NecessaryEffects, quest.OptionalEffects);
                }
            }
            else
            {
                MelonLogger.Error("❌ Failed to create QuestDelivery instance - Accept Quest.");
                return;
            }
            MelonLogger.Msg($"✅ Quest accepted: {quest.Title}");
            // Remove quest from the UI and underlying list
            if (questListContainer != null)
            {
                ClearChild(questListContainer, quest.Index);
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