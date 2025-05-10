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
                Contacts.Initialize();
                statusText.text = "✅ Dealers initialized";
            }
            catch (Exception ex)
            {
                statusText.text = "❌ Failed to initialize dealers";
                MelonLogger.Error($"Failed to initialize dealers: {ex}");
            }
        }
        protected override void OnCreatedUI(GameObject container)
        {

            var bg = UIFactory.Panel("MainBG", container.transform, Color.black, fullAnchor: true);

            UIFactory.TopBar(name: "TopBar",
                parent: bg.transform,
                title: "Silk Road",
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

            MelonCoroutines.Start(WaitForBuyerAndInitialize());
        }

        private void OpenManageUI(GameObject bg)
        {
            var managementPanel = UIFactory.Panel("ManagementPanel", bg.transform, new Color(200, 200, 200, 0.3f), fullAnchor: true);
            managementPanel.gameObject.SetActive(true);

            managementPanel.transform.SetAsLastSibling();

            var (contentBackground, topBar, buttonRow) = SetupInitialPanel(managementPanel);

            var detailsPanel = UIFactory.Panel("DetailsPanel", managementPanel.transform, new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0, 0), new Vector2(1, 0.82f));

            var leftPanel = UIFactory.Panel("QuestListPanel", detailsPanel.transform, new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0.02f, 0.05f), new Vector2(0.49f, 0.82f));
            questListContainer = UIFactory.ScrollableVerticalList("QuestListScroll", leftPanel.transform, out _);
            UIFactory.FitContentHeight(questListContainer);

            var rightPanel = UIFactory.Panel("DetailPanel", detailsPanel.transform, new Color(0.12f, 0.12f, 0.12f),
                new Vector2(0.49f, 0f), new Vector2(0.98f, 0.82f));

            // Use vertical layout with padding and spacing like Tax & Wash
            UIFactory.VerticalLayoutOnGO(rightPanel, spacing: 14, padding: new RectOffset(24, 50, 15, 70));
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

        private System.Collections.IEnumerator WaitForBuyerAndInitialize()
        {
            float timeout = 5f;
            float waited = 0f;

            MelonLogger.Msg("PhoneApp-WaitForBuyerAndInitialize-Waiting for save buyer to be initialized...");
            var savebuyer = Contacts.GetBuyer(BlackmarketBuyer.SavedNPCName);
            //Melonlogger - number of elements in contacts.Buyers
            //MelonLogger.Msg($"1Contacts.Buyers Count: {Contacts.Buyers.Count}");

            // Wait until Contacts.Buyers is initialized and all buyers are marked as initialized, or until the timeout is reached
            while ((savebuyer == null || !savebuyer.IsInitialized) && waited < timeout)
            {
                savebuyer = Contacts.GetBuyer(BlackmarketBuyer.SavedNPCName);
                waited += Time.deltaTime;
                yield return null; // Wait for the next frame
            }

            // Check if the timeout was reached
            if (savebuyer == null || !savebuyer.IsInitialized)
            {
                MelonLogger.Warning("⚠️ PhoneApp-Timeout reached. Save buyer is still not initialized.");
                yield break; // Exit the coroutine
            }
            //Melonlogger - number of elements in contacts.Buyers
            //MelonLogger.Msg($"4Contacts.Buyers Count: {Contacts.Buyers.Count}");
            // Log that the default buyer is initialized
            MelonLogger.Msg($"✅ Default Buyer with save data initialized: {Contacts.Buyers.Count} buyers found.");

            // Call InitializeDealers after save data buyers is initialized
            InitializeDealers();
            MelonLogger.Msg("Dealers initialized successfully.");
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
                if (buyer.DealerName==BlackmarketBuyer.SavedNPCName){
                    MelonLogger.Warning($"⚠️ Dealer {buyer.DealerName} found in Buyers dictionary. Not progressing further");
                    continue;
                }
                var shipping = buyer.Shippings[dealerSaveData.ShippingTier];
                // Log dealer information
                MelonLogger.Msg($"✅ Processing dealer: {buyer.DealerName}");
                MelonLogger.Msg($"   Unlocked Drugs: {string.Join(", ", dealerSaveData.UnlockedDrugs)}");
                MelonLogger.Msg($"   MinDeliveryAmount: {shipping.MinAmount}, MaxDeliveryAmount: {shipping.MaxAmount}");

                //drugTypes are unique dealerSaveData.UnlockedDrugs.Type
                var drugTypes = dealerSaveData.UnlockedDrugs.Select(d => d.Type).Distinct().ToArray();

                // Iterate through unlocked drugs and generate a quest for each drugTypes in dealerSaveData
                foreach (var drugType in drugTypes)
                {
                    if (dealerSaveData.UnlockedDrugs.Any(d => d.Type == drugType))
                    {
                        GenerateQuest(buyer, dealerSaveData, drugType);
                    }
                }
            }

            // Log the total number of quests loaded
            MelonLogger.Msg($"✅ Total quests loaded: {quests.Count}");

            // Refresh the UI to display the quests
            RefreshQuestList();
        }





        private void GenerateQuest(BlackmarketBuyer buyer, DealerSaveData dealerSaveData, string drugType)
        {
            var shipping = buyer.Shippings[dealerSaveData.ShippingTier];
            int minAmount= shipping.MinAmount;
            int maxAmount= shipping.MaxAmount;
            if (buyer.RepLogBase!=0){
                double logResult = Math.Log((double)buyer._DealerData.Reputation, (double)buyer.RepLogBase);
                minAmount = (int)(minAmount * (1+logResult));
                maxAmount = (int)(maxAmount * (1+logResult));
            }
            //Setting order amount
            int steps = (maxAmount - minAmount) / shipping.StepAmount;
            int randomStep = RandomUtils.RangeInt(0, steps);
            int amount = minAmount + randomStep * shipping.StepAmount;
            //Iterate through unlocked drugs where drug type is the same as the one passed in
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
            var lastQuality = randomDrug.Qualities.LastOrDefault();
            var qualityMult = 0f;
            if (lastQuality != null)
            {
                quality = lastQuality.Type;
                qualityMult = lastQuality.DollarMult;
                aggregateDollarMultMin = (1 + lastQuality.DollarMult) * dealTimesMult;
                aggregateDollarMultMax = aggregateDollarMultMin;

            }
            var tempMult11 = 1f;//min

            var tempMult21 = 1f;//max


            //Iterate through randomDrug.Effects and check if the effect is necessary or optional. Also multiply aggregate dollar and rep multipliers with base dollar+sum of effects dollar mult. Same for rep.

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
                        necessaryEffectMult.Add(effect.DollarMult);
                        tempMult11 += effect.DollarMult;
                        tempMult21 += effect.DollarMult;
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
                            necessaryEffectMult.Add(effect.DollarMult);
                            tempMult11 += effect.DollarMult;
                            tempMult21 += effect.DollarMult;
                        }
                    }

                }
                else if (effect.Probability > 0f && effect.Probability <= 1f && UnityEngine.Random.Range(0f, 1f) < effect.Probability)
                {
                    if (effect.Name != "Random")
                    {
                        optionalEffects.Add(effect.Name);
                        optionalEffectMult.Add(effect.DollarMult);
                        tempMult21 += effect.DollarMult;
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
                            optionalEffectMult.Add(effect.DollarMult);
                            tempMult21 += effect.DollarMult;
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



            var quest = new QuestData
            {
                Title = $"{buyer.DealerName} wants {drugType} delivered.",
                Task = $"Deliver {amount}x {quality} {drugType}" + (effectDesc.Length > 0 ? $" with [{effectDesc}]" : ""),
                ProductID = drugType,
                AmountRequired = (uint)amount,
                TargetObjectName = buyer.DealerName,
                DealerName = buyer.DealerName,
                QuestImage = Path.Combine(MelonEnvironment.ModsDirectory, "Empire", buyer.DealerImage ?? "EmpireIcon_quest.png"),
                BaseDollar = randomDrug.BaseDollar,
                BaseRep = randomDrug.BaseRep,
                BaseXp = randomDrug.BaseXp,
                RepMult = randomDrug.RepMult,
                XpMult = randomDrug.XpMult,
                DollarMultiplierMin = (float)Math.Round(aggregateDollarMultMin, 2),
                DollarMultiplierMax = (float)Math.Round(aggregateDollarMultMax, 2),

                DealTime = dealTime,
                DealTimeMult = dealTimesMult,
                Penalties = new List<int> { (int)(buyer.Deals[randomIndex][2] * shipping.DealModifier[2]), (int)(buyer.Deals[randomIndex][3] * shipping.DealModifier[3]) },

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
            Index = 0;
            foreach (var quest in quests)
            {
                if (quest == null) continue;

                //MelonLogger.Msg($"✅ Adding quest to UI: {quest.Title}");

                var row = UIFactory.CreateQuestRow(quest.Title, questListContainer, out var iconPanel, out var textPanel);
                UIFactory.SetIcon(ImageUtils.LoadImage(quest.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png")), iconPanel.transform);
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
            questReward.text = $" Rewards: ${quest.BaseDollar} + Pricex({quest.DollarMultiplierMin} - {quest.DollarMultiplierMax})\n" +
                $"Rep :{quest.BaseRep} + Dollarx{quest.RepMult}\n\n" +
                $"XP :{quest.BaseXp} + Dollarx{quest.XpMult}\n\n" +
                $"Deal Expiry: {quest.DealTime} Day(s)\n" +
                $"Failure Penalties: ${quest.Penalties[0]} + {quest.Penalties[1]} Rep\n";
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
            //MelonLogger.Msg($"✅ Test 213: ");
            if (q is QuestDelivery delivery)
            {
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
                QuestDelivery.Active = delivery; // ✅ FIX: set Active manually here

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
            if (questListContainer != null)
                ClearChild(questListContainer, quest.Index);
            else
                MelonLogger.Warning("questListContainer is null in AcceptQuest, skipping ClearChild.");
            ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color32(32, 0x82, 0xF6, 0xff));
            acceptButton.interactable = false;
            ButtonUtils.Enable(cancelButton, cancelLabel, "Cancel Current Delivery");
        }
    }
}