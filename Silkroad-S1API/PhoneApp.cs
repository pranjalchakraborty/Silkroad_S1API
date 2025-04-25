using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using S1API.UI;
using S1API.Utils;
using SilkRoad;
using System.Linq;
using MelonLoader.Utils;
using System.IO;
using S1API.Internal.Utils;

namespace Silkroad
{
    public class MyApp : S1API.PhoneApp.PhoneApp
    {   
        protected override string AppName => "Silkroad";
        protected override string AppTitle => "Silkroad";
        protected override string IconLabel => "Silkroad";
        protected override string IconFileName => Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon.png");

        private List<QuestData> quests;
        private RectTransform questListContainer;
        private Text questTitle, questTask, questReward, deliveryStatus, acceptLabel;
        private Button acceptButton;
        private Text statusText;

        //Bypass method to set quest image dynamically from dealer icon
        public static string QuestImage;

        protected override void OnCreatedUI(GameObject container)
        {
            var bg = UIFactory.Panel("MainBG", container.transform, Color.black, fullAnchor: true);

            // Top bar with refresh button
            UIFactory.TopBar("TopBar", bg.transform, "Silk Road", 150f, 10f, 0.82f, 75, 75, 0, 35,
                () =>
                {
                    RefreshQuestList();
                    LoadQuests();
                }, "Refresh Quests");

            // Status text below top bar
            statusText = UIFactory.Text("Status", "", bg.transform, 14);
            statusText.rectTransform.anchorMin = new Vector2(0.7f, 0.85f);
            statusText.rectTransform.anchorMax = new Vector2(0.98f, 0.9f);
            statusText.alignment = TextAnchor.MiddleRight;

            var leftPanel = UIFactory.Panel("QuestListPanel", bg.transform, new Color(0.1f, 0.1f, 0.1f),
                new Vector2(0.02f, 0f), new Vector2(0.49f, 0.82f));
            questListContainer = UIFactory.ScrollableVerticalList("QuestListScroll", leftPanel.transform, out _);
            UIFactory.FitContentHeight(questListContainer);

            var rightPanel = UIFactory.Panel("DetailPanel", bg.transform, new Color(0.12f, 0.12f, 0.12f),
                new Vector2(0.49f, 0f), new Vector2(0.98f, 0.82f));

            UIFactory.VerticalLayoutOnGO(rightPanel, spacing: 12, padding: new RectOffset(10, 40, 10, 65));

            questTitle = UIFactory.Text("Title", "Select a quest", rightPanel.transform, 22, TextAnchor.UpperLeft, FontStyle.Bold);
            questTask = UIFactory.Text("Task", "Task: --", rightPanel.transform, 18);
            questReward = UIFactory.Text("Reward", "Reward: --", rightPanel.transform, 18);
            deliveryStatus = UIFactory.Text("Delivery", "", rightPanel.transform, 16);

            var (acceptGO, acceptBtn, acceptLbl) = UIFactory.ButtonWithLabel("AcceptBtn", "Accept Delivery", rightPanel.transform, new Color(0.2f, 0.6f, 0.2f), 160, 100);
            acceptButton = acceptBtn;
            acceptLabel = acceptLbl;

            // Automatically initialize dealers
            InitializeDealers();
            MelonLogger.Msg(statusText.text);
            LoadQuests();
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

        private void LoadQuests()
        {
            quests = new List<QuestData>();

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
                if (!BlackmarketBuyer.Buyers.TryGetValue(buyer.DealerName, out var dealerSaveData))
                {
                    MelonLogger.Warning($"⚠️ Dealer {buyer.DealerName} not found in Buyers dictionary.");
                    continue;
                }

                // Log dealer information
                MelonLogger.Msg($"✅ Processing dealer: {dealerSaveData.DealerName}");
                MelonLogger.Msg($"   Unlocked Drugs: {string.Join(", ", dealerSaveData.UnlockedDrugs)}");
                MelonLogger.Msg($"   MinDeliveryAmount: {dealerSaveData.MinDeliveryAmount}, MaxDeliveryAmount: {dealerSaveData.MaxDeliveryAmount}");
                MelonLogger.Msg($"   Necessary Effects: {string.Join(", ", dealerSaveData.NecessaryEffects.Select(e => e.Name))}");
                MelonLogger.Msg($"   Optional Effects: {string.Join(", ", dealerSaveData.OptionalEffects.Select(e => e.Name))}");

                // Iterate through unlocked drugs
                foreach (var drugType in dealerSaveData.UnlockedDrugs)
                {
                    if (!dealerSaveData.UnlockedQuality.TryGetValue(drugType, out var qualityType))
                    {
                        MelonLogger.Warning($"⚠️ No quality unlocked for drug {drugType}.");
                        continue;
                    }

                    MelonLogger.Msg($"   Processing drug: {drugType} (Quality: {qualityType})");

                    // Get necessary effects that are unlocked (rep requirement met)
                    var necessaryEffects = dealerSaveData.NecessaryEffects?
                        .Where(e => e.UnlockRep <= dealerSaveData.Reputation)
                        .Select(e => e.Name)
                        .Distinct()
                        .ToList() ?? new List<string>();

                    // Get optional effects that are unlocked and roll for each
                    var optionalEffects = new List<string>();
                    if (dealerSaveData.OptionalEffects != null)
                    {
                        foreach (var effect in dealerSaveData.OptionalEffects.Where(e => e.UnlockRep <= dealerSaveData.Reputation))
                        {
                            if (UnityEngine.Random.Range(0f, 1f) < effect.Probability)
                            {
                                optionalEffects.Add(effect.Name);
                                MelonLogger.Msg($"      Optional effect {effect.Name} rolled in.");
                            }
                            else
                            {
                                MelonLogger.Msg($"      Optional effect {effect.Name} did not roll.");
                            }
                        }
                    }

                    // Only generate a quest if there is at least one effect
                    if (necessaryEffects.Count > 0 || optionalEffects.Count > 0)
                    {
                        GenerateQuest(dealerSaveData, drugType, qualityType, necessaryEffects, optionalEffects);
                    }
                    else
                    {
                        MelonLogger.Msg($"      No effects for {drugType}, skipping quest generation.");
                    }
                }
            }

            // Log the total number of quests loaded
            MelonLogger.Msg($"✅ Total quests loaded: {quests.Count}");

            // Refresh the UI to display the quests
            RefreshQuestList();
        }





        private void GenerateQuest(DealerSaveData dealer, string drugType, string quality, List<string> necessaryEffects, List<string> optionalEffects)
        {
            // Ensure that any string parameter is not null by defaulting it to empty string.
            dealer.DealerName = dealer.DealerName ?? "";
            drugType = drugType ?? "";
            quality = quality ?? "";
            necessaryEffects = necessaryEffects ?? new List<string>();
            optionalEffects = optionalEffects ?? new List<string>();

            int amount = RandomUtils.RangeInt(dealer.MinDeliveryAmount, dealer.MaxDeliveryAmount);
            float effectMultiplier = (necessaryEffects.Count > 0) ? 1.2f : 1.0f;
            int reward = Mathf.RoundToInt(drugType.Length * 20f * amount * effectMultiplier);

            string effectDesc = "";
            if (necessaryEffects.Count > 0)
                effectDesc += $"Required: {string.Join(", ", necessaryEffects)}";
            if (optionalEffects.Count > 0)
                effectDesc += (effectDesc.Length > 0 ? "; " : "") + $"Optional: {string.Join(", ", optionalEffects)}";

            var productID = drugType;
            //Append productID with quality, necessary effects and optional effects
            if (quality.Length > 0)
                productID += $"{quality}";

            if (necessaryEffects.Count > 0)
                productID += $"NE:{string.Join(",", necessaryEffects)}";

            if (optionalEffects.Count > 0)
                productID += $"OE:{string.Join(",", optionalEffects)}";

            var quest = new QuestData
            {
                Title = $"{dealer.DealerName} - {drugType} Delivery",
                Task = $"Deliver {amount}x {quality} {drugType}" + (effectDesc.Length > 0 ? $" with [{effectDesc}]" : ""),
                Reward = reward,
                ProductID = drugType,
                AmountRequired = (uint)amount,
                TargetObjectName = dealer.DealerName,
                DealerName = dealer.DealerName,
                NecessaryEffects = necessaryEffects,
                OptionalEffects = optionalEffects,
                QuestImage=Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", BlackmarketBuyer.GetDealerSaveData(dealer.DealerName)?.Icon ?? "SilkRoadIcon_quest.png") 
            };

            quests.Add(quest);

            MelonLogger.Msg($"✅ Quest generated:");
            MelonLogger.Msg($"   Title: {quest.Title}");
            MelonLogger.Msg($"   Task: {quest.Task}");
            MelonLogger.Msg($"   Reward: ${quest.Reward}");
            MelonLogger.Msg($"   Amount Required: {quest.AmountRequired}");
            MelonLogger.Msg($"   Necessary Effects: {string.Join(", ", quest.NecessaryEffects)}");
            MelonLogger.Msg($"   Optional Effects: {string.Join(", ", quest.OptionalEffects)}");
        }


        private void RefreshQuestList()
        {
            UIFactory.ClearChildren(questListContainer);

            foreach (var quest in quests)
            {
                if (quest == null) continue;

                MelonLogger.Msg($"✅ Adding quest to UI: {quest.Title}");

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
            questReward.text = $"Reward: ${quest.Reward:N0}";
            deliveryStatus.text = "";
            ButtonUtils.Enable(acceptButton, acceptLabel, "Accept Delivery");
            ButtonUtils.ClearListeners(acceptButton);
            ButtonUtils.AddListener(acceptButton, () => AcceptQuest(quest));
        }

        private void AcceptQuest(QuestData quest)
        {
            if (QuestDelivery.QuestActive)
            {
                deliveryStatus.text = "⚠️ Finish your current job first!";
                return;
            }
            QuestImage=BlackmarketBuyer.GetDealerSaveData(quest.DealerName)?.Icon;
            var q = S1API.Quests.QuestManager.CreateQuest<QuestDelivery>();
            if (q is QuestDelivery delivery)
            {
                delivery.Data.ProductID = quest.ProductID;
                delivery.Data.RequiredAmount = quest.AmountRequired;
                delivery.Data.Reward = quest.Reward;
                delivery.Data.DealerName = quest.DealerName;
                delivery.Data.NecessaryEffects = quest.NecessaryEffects;
                delivery.Data.OptionalEffects = quest.OptionalEffects;
                delivery.Data.QuestImage = QuestImage;

                if (Contacts.GetBuyer(quest.DealerName) is BlackmarketBuyer buyer)
                {
                    buyer.SendDeliveryAccepted(quest.DealerName, quest.ProductID, (int)quest.AmountRequired);
                }
            }

            deliveryStatus.text = "📦 Delivery started!";
        }
    }
}