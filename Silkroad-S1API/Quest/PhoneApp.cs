using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using S1API.UI;
using S1API.Console;
using S1API.GameTime;
using Silkroad;
using System.Linq;
using MelonLoader.Utils;
using System.IO;
using S1API.Internal.Utils;
using Object = UnityEngine.Object;
using S1API.Money;

namespace Silkroad
{

    public class MyApp : S1API.PhoneApp.PhoneApp
    {
        public static BlackmarketBuyer saveBuyer { get; set; }
        protected override string AppName => "Silkroad";
        protected override string AppTitle => "Silkroad";
        protected override string IconLabel => "Silkroad";
        protected override string IconFileName => Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon.png");

        private List<QuestData> quests;
        private RectTransform questListContainer;
        private Text questTitle, questTask, questReward, deliveryStatus, acceptLabel, cancelLabel, refreshLabel, manageLabel, relationsLabel;
        private Button acceptButton, cancelButton, refreshButton, manageButton, relationsButton;
        private Text statusText;
        public static int Index;

        //Bypass method to set quest image dynamically from dealer icon - not used - TODO
        public static string QuestImage;
        protected override void OnCreated()
        {
            base.OnCreated();
            MelonLogger.Msg("[SilkRoadApp] OnCreated called");
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

            // Top bar with refresh button
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
            deliveryStatus.color = new Color(0.7f, 0.9f, 0.7f);
            // Create a horizontal container for Refresh and Cancel
            var topButtonRow = UIFactory.Panel("TopButtonRow", rightPanel.transform, Color.clear);
            UIFactory.HorizontalLayoutOnGO(topButtonRow, spacing: 12);
            UIFactory.SetLayoutGroupPadding(topButtonRow.GetComponent<HorizontalLayoutGroup>(), 0, 0, 0, 0);

            // Create horizontal row for top buttons
            var buttonRow = UIFactory.ButtonRow("TopButtons", rightPanel.transform, spacing: 14);

            //Manage button
            var (manageGO, manageBtn, ManageLbl) = UIFactory.RoundedButtonWithLabel("ManageBtn", "Manage", rightPanel.transform, new Color32(32, 0x82, 0xF6, 0xff), 460f, 60f, 22, Color.black);

            manageButton = manageBtn;
            manageLabel = ManageLbl;

            ButtonUtils.AddListener(manageButton, () => OpenManageUI(bg));

            // Cancel Button

            var (cancelGO, cancelBtn, cancelLbl) = UIFactory.RoundedButtonWithLabel("CancelBtn", "Cancel current Delivery", buttonRow.transform, new Color32(32, 0x82, 0xF6, 0xff), 300, 90f, 18, Color.black);
            cancelButton = cancelBtn;
            cancelLabel = cancelLbl;
            if (!QuestDelivery.QuestActive)
                ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");

            // Accept Button (separate row)
            var (acceptGO, acceptBtn, acceptLbl) = UIFactory.RoundedButtonWithLabel("AcceptBtn", "No quest selected", rightPanel.transform, new Color32(32, 0x82, 0xF6, 0xff), 460f, 60f, 22, Color.black);


            acceptButton = acceptBtn;
            acceptLabel = acceptLbl;
            ButtonUtils.Disable(acceptBtn, acceptLabel, "No quest selected");

            // Refresh Button
            var (refreshGO, refreshBtn, refreshLbl) = UIFactory.RoundedButtonWithLabel("RefreshBtn", "Refresh Order list", buttonRow.transform, new Color32(32, 0x82, 0xF6, 0xff), 300, 90, 18, Color.black);
            refreshButton = refreshBtn;
            refreshLabel = refreshLbl;

            ButtonUtils.AddListener(refreshButton, () => RefreshButton());

            MelonCoroutines.Start(WaitForBuyerAndInitialize());
        }

        private void OpenManageUI(GameObject bg)
        {
            // Create a modal panel
            var managementPanel = UIFactory.Panel("ManagementPanel", bg.transform, new Color(200, 200, 200, 0.3f), fullAnchor: true);
            managementPanel.gameObject.SetActive(true);

            managementPanel.transform.SetAsLastSibling();

            // Add a background to the modal content
            var contentBackground = UIFactory.Panel("ContentBackground", managementPanel.transform, new Color(0.2f, 0.2f, 0.2f, 1f));
            var contentRect = contentBackground.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.0f, 0.0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // Add a top bar to the modal
            var topBar = UIFactory.Panel("TopBar", managementPanel.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
            var topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0f, 0.9f); // Top 10% of the modal
            topBarRect.anchorMax = new Vector2(1f, 1f);   // Full width
            topBarRect.offsetMin = Vector2.zero;
            topBarRect.offsetMax = Vector2.zero;

            var buttonRow = UIFactory.ButtonRow("TopButtons", topBar.transform, spacing: 14);

            // Add a "Relations" button to the top bar
            var (relationsGO, relationsBtn, relationsLbl) = UIFactory.RoundedButtonWithLabel(
                "RelationsButton",
                "Relations",
                buttonRow.transform,
                new Color(0.5f, 0.2f, 0.2f, 1f), // Button color
                100, // Width
                100, // Height
                18, // Font size
                Color.white // Text color
            );

            var relationsRect = relationsGO.GetComponent<RectTransform>();
            relationsRect.anchorMin = new Vector2(0.05f, 0.5f);
            relationsRect.anchorMax = new Vector2(0.05f, 0.5f);
            relationsRect.anchoredPosition = new Vector2(10f, 10f);
            relationsRect.sizeDelta = new Vector2(100, 60);

            relationsButton = relationsBtn;
            relationsLabel = relationsLbl;

            //Test with another button
            var (relationsGO2, relationsBtn2, relationsLbl2) = UIFactory.RoundedButtonWithLabel(
                "RelationsButton2",
                "Relations2",
                buttonRow.transform,
                new Color(0.5f, 0.2f, 0.2f, 1f), // Button color
                100, // Width
                100, // Height
                18, // Font size
                Color.white // Text color
            );

            // Position the button in the top bar
            var relationsRect2 = relationsGO2.GetComponent<RectTransform>();
            relationsRect2.anchorMin = new Vector2(0.05f, 0.5f);
            relationsRect2.anchorMax = new Vector2(0.05f, 0.5f);
            relationsRect2.anchoredPosition = new Vector2(10f, 10f);
            relationsRect2.sizeDelta = new Vector2(100, 60);

            // Add a close button (red "X") to the top-right corner
            var (closeGO, closeBtn, closeLbl) = UIFactory.RoundedButtonWithLabel(
                "CloseButton",
                "X",
                managementPanel.transform,
                new Color32(235, 53, 56, 255), // Red color
                50, // Width
                20, // Height
                12, // Font size
                Color.white // Text color
            );

            // Position the close button in the top-right corner
            var closeRect = closeGO.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.98f, 0.98f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);
            closeRect.sizeDelta = new Vector2(25, 25);

            // Add a listener to the close button to destroy the modal panel
            ButtonUtils.AddListener(closeBtn, () => Object.Destroy(managementPanel.gameObject));

            // Add content to the modal
            var description = UIFactory.Text(
                "ModalDescription",
                "This description text will be replaced with a panel full of detailed information, default relations.",
                managementPanel.transform,
                18,
                TextAnchor.UpperLeft,
                FontStyle.Normal
            );

            // Position the description
            var descriptionRect = description.rectTransform;
            descriptionRect.anchorMin = new Vector2(0.1f, 0.1f);
            descriptionRect.anchorMax = new Vector2(0.9f, 0.7f);
            descriptionRect.offsetMin = Vector2.zero;
            descriptionRect.offsetMax = Vector2.zero;

            // Set listeners on navigation buttons to show descriptions
            ButtonUtils.AddListener(relationsButton, () => SetDetailsContent(description, "Relations"));
        }

        private void SetDetailsContent(Text description, string tab)
        {
            switch (tab)
            {
                case "Relations":
                    description.text = "Relations content goes here.";
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

            MelonLogger.Msg("WaitForBuyerAndInitialize-Waiting for buyers to be initialized...");

            // Wait until Contacts.Buyers is initialized and all buyers are marked as initialized, or until the timeout is reached
            while ((Contacts.Buyers == null || Contacts.Buyers.Count == 0 || !Contacts.Buyers.Values.All(buyer => buyer.IsInitialized)) && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null; // Wait for the next frame
            }

            // Check if the timeout was reached
            if (Contacts.Buyers == null || Contacts.Buyers.Count == 0 || !Contacts.Buyers.Values.All(buyer => buyer.IsInitialized))
            {
                MelonLogger.Warning("⚠️ Timeout reached. Some buyers are still not initialized.");
                
                // Log uninitialized buyers
                if (Contacts.Buyers != null)
                {
                    foreach (var buyer in Contacts.Buyers.Values.Where(b => !b.IsInitialized))
                    {
                        MelonLogger.Warning($"Buyer not initialized: {buyer.DealerName}");
                    }
                }
                yield break; // Exit the coroutine
            }

            // Log the count of initialized buyers
            MelonLogger.Msg($"✅ Buyer with save data initialized: {Contacts.Buyers.Count} buyers found.");
            
            // Call InitializeDealers after all buyers are initialized
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
            RefreshQuestList();
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

                // Log dealer information
                MelonLogger.Msg($"✅ Processing dealer: {dealerSaveData.DealerName}");
                MelonLogger.Msg($"   Unlocked Drugs: {string.Join(", ", dealerSaveData.UnlockedDrugs)}");
                MelonLogger.Msg($"   MinDeliveryAmount: {dealerSaveData.MinDeliveryAmount}, MaxDeliveryAmount: {dealerSaveData.MaxDeliveryAmount}");

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
            //Setting order amount
            int steps = (dealerSaveData.MaxDeliveryAmount - dealerSaveData.MinDeliveryAmount) / dealerSaveData.StepDeliveryAmount;
            int randomStep = RandomUtils.RangeInt(0, steps);
            int amount = dealerSaveData.MinDeliveryAmount + randomStep * dealerSaveData.StepDeliveryAmount;
            //Iterate through unlocked drugs where drug type is the same as the one passed in
            var unlockedDrugs = dealerSaveData.UnlockedDrugs.Where(d => d.Type == drugType).ToList();


            if (unlockedDrugs.Count == 0)
            {
                MelonLogger.Warning($"⚠️ No unlocked drugs of type {drugType} found for dealer {dealerSaveData.DealerName}.");
                return;
            }
            var necessaryEffects = new List<string>();
            var optionalEffects = new List<string>();
            var quality = "";
            var aggregateDollarMultMin = 0f;
            var aggregateDollarMultMax = 0f;
            var aggregateRepMultMin = 0f;
            var aggregateRepMultMax = 0f;
            var randomIndex = UnityEngine.Random.Range(0, buyer.DealTimes.Count);
            var dealTime = buyer.DealTimes[randomIndex];
            var dealTimesMult = buyer.DealTimesMult[randomIndex];
            //Get a random drug from the unlocked drugs
            var randomDrug = unlockedDrugs[RandomUtils.RangeInt(0, unlockedDrugs.Count)];
            //Store the last quality. Also store dollar and rep multiplier
            var lastQuality = randomDrug.Qualities.LastOrDefault();
            if (lastQuality != null)
            {
                quality = lastQuality.Type;
                aggregateDollarMultMin = (1 + lastQuality.DollarMult) * (1 + dealTimesMult);
                aggregateRepMultMin = (1 + lastQuality.RepMult) * (1 + dealTimesMult);
                aggregateDollarMultMax = aggregateDollarMultMin;
                aggregateRepMultMax = aggregateRepMultMin;

            }
            var tempMult11 = randomDrug.BaseDollarMult;//min
            var tempMult12 = randomDrug.BaseRepMult;//min
            var tempMult21 = randomDrug.BaseDollarMult;//max
            var tempMult22 = randomDrug.BaseRepMult;//max
            //Iterate through randomDrug.Effects and check if the effect is necessary or optional. Also multiply aggregate dollar and rep multipliers with base dollar+sum of effects dollar mult. Same for rep.
            foreach (var effect in randomDrug.Effects)
            {
                if (effect.Probability == 1)
                {
                    necessaryEffects.Add(effect.Name);
                    tempMult11 += effect.DollarMult;
                    tempMult12 += effect.RepMult;
                    tempMult21 += effect.DollarMult;
                    tempMult22 += effect.RepMult;
                }
                else
                {
                    //Roll Optional Effects by their probability to see if they are removed from randomDrug
                    if (UnityEngine.Random.Range(0f, 1f) < effect.Probability)
                    {
                        optionalEffects.Add(effect.Name);
                        tempMult21 += effect.DollarMult;
                        tempMult22 += effect.RepMult;

                    }

                }
            }
            aggregateDollarMultMin *= tempMult11;
            aggregateRepMultMin *= tempMult12;
            aggregateDollarMultMax *= tempMult21;
            aggregateRepMultMax *= tempMult22;
            //remove from randomDrug.Effects the optional effects that are not in the list of optional effects and have a probability < 1f
            randomDrug.Effects.RemoveAll(effect => !optionalEffects.Contains(effect.Name) && effect.Probability < 1f);

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
                Title = $"{buyer.DealerName} - {drugType} Delivery",
                Task = $"Deliver {amount}x {quality} {drugType}" + (effectDesc.Length > 0 ? $" with [{effectDesc}]" : ""),
                ProductID = drugType,
                AmountRequired = (uint)amount,
                TargetObjectName = buyer.DealerName,
                DealerName = buyer.DealerName,
                RequiredDrug = randomDrug,
                QuestImage = Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", buyer.DealerImage ?? "SilkRoadIcon_quest.png"),
                BonusDollar = randomDrug.BonusDollar,
                BonusRep = randomDrug.BonusRep,
                DollarMultiplierMin = aggregateDollarMultMin,
                RepMultiplierMin = aggregateRepMultMin,
                DollarMultiplierMax = aggregateDollarMultMax,
                RepMultiplierMax = aggregateRepMultMax,
                DealTime = dealTime,
                DealTimeMult = dealTimesMult,
                Penalties = buyer.Penalties,
                Quality = quality,
                NecessaryEffects = necessaryEffects,
                OptionalEffects = optionalEffects,
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
                //RefreshQuestList();//No Free Refresh - TODO
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
                UIFactory.SetIcon(ImageUtils.LoadImage(quest.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png")), iconPanel.transform);
                ButtonUtils.AddListener(row.GetComponent<Button>(), () => OnSelectQuest(quest));

                UIFactory.CreateTextBlock(textPanel.transform, quest.Title, quest.Task,
                    QuestDelivery.CompletedQuestKeys?.Contains($"{quest.ProductID}_{quest.AmountRequired}") == true);
            }
        }
        private void OnSelectQuest(QuestData quest)
        {
            questTitle.text = quest.Title;
            questTask.text = $"Task: {quest.Task}";
            questReward.text = $" Rewards: ${quest.BonusDollar} + Pricex({quest.DollarMultiplierMin} - {quest.DollarMultiplierMax})\n" +
                $"Rep :{quest.BonusRep} + Pricex({quest.RepMultiplierMin} - {quest.RepMultiplierMax})\n\n" +
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
                ButtonUtils.Enable(acceptButton, acceptLabel, "In Progress");
                ButtonUtils.ClearListeners(acceptButton);
                ButtonUtils.AddListener(acceptButton, () => AcceptQuest(quest));
                ButtonUtils.Enable(cancelButton, cancelLabel, "Cancel Current Delivery");
                ButtonUtils.ClearListeners(cancelButton);
                ButtonUtils.AddListener(cancelButton, () => CancelCurrentQuest(quest));
            }

            //ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");
            ButtonUtils.ClearListeners(cancelButton);
            ButtonUtils.AddListener(cancelButton, () => CancelCurrentQuest(quest));
            ButtonUtils.Enable(refreshButton, refreshLabel, "Refresh Order List");
            ButtonUtils.ClearListeners(refreshButton);
            ButtonUtils.AddListener(refreshButton, () => RefreshButton());

        }


        private void AcceptQuest(QuestData quest)
        {
            if (QuestDelivery.QuestActive)
            {
                deliveryStatus.text = "⚠️ Finish your current job first!";
                ButtonUtils.Disable(acceptButton, acceptLabel, "In Progress");
                ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color32(0x91, 0xFF, 0x8E, 0xff));
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
                delivery.Data.ProductID = quest.ProductID;
                delivery.Data.RequiredAmount = quest.AmountRequired;
                delivery.Data.DealerName = quest.DealerName;
                delivery.Data.QuestImage = quest.QuestImage;
                delivery.Data.RequiredDrug = quest.RequiredDrug;
                delivery.Data.Reward = quest.BonusDollar;
                delivery.Data.RepReward = quest.BonusRep;
                delivery.Data.Task = quest.Task;
                delivery.Data.DealTime = quest.DealTime;
                delivery.Data.DealTimeMult = quest.DealTimeMult;
                delivery.Data.Penalties = quest.Penalties;
                delivery.Data.Quality = quest.Quality;
                delivery.Data.NecessaryEffects = quest.NecessaryEffects;
                delivery.Data.OptionalEffects = quest.OptionalEffects;
                QuestDelivery.Active = delivery; // ✅ FIX: set Active manually here

                if (Buyer is BlackmarketBuyer buyer)
                {
                    buyer.SendCustomMessage("Accept", quest.ProductID, (int)quest.AmountRequired);
                }
            }
            else
            {
                MelonLogger.Error("❌ Failed to create QuestDelivery instance - Accept Quest.");
                return;
            }
            MelonLogger.Msg($"✅ Quest accepted: {quest.Title}");
            ClearChild(questListContainer, quest.Index);
            ButtonUtils.SetStyle(acceptButton, acceptLabel, "In Progress", new Color32(0x91, 0xFF, 0x8E, 0xff));
            acceptButton.interactable = false;
            ButtonUtils.Enable(cancelButton, cancelLabel, "Cancel Current Delivery");
        }
    }
}