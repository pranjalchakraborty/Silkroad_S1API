using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using S1API.UI;
using S1API.Utils;
using Silkroad;
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

                // Iterate through unlocked drugs and generate a quest for each
                foreach (var drug in dealerSaveData.UnlockedDrugs)
                {
                    GenerateQuest(buyer, dealerSaveData, drug.Type);
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
            //Get a random drug from the unlocked drugs
            var randomDrug = unlockedDrugs[RandomUtils.RangeInt(0, unlockedDrugs.Count)];
            //Store the last quality. Also store 1+ dollar and rep multiplier
            var lastQuality = randomDrug.Qualities.LastOrDefault();
            if (lastQuality != null)
            {
                quality = lastQuality.Type;
                aggregateDollarMultMin = 1 + lastQuality.DollarMult;
                aggregateRepMultMin = 1 + lastQuality.RepMult;
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
            };

            quests.Add(quest);

            //MelonLogger.Msg($"✅ Quest generated:");
            //MelonLogger.Msg($"   Title: {quest.Title}");
            //MelonLogger.Msg($"   Task: {quest.Task}");
            //MelonLogger.Msg($"   Amount Required: {quest.AmountRequired}");
            //MelonLogger.Msg($"   Required Drug: {quest.RequiredDrug}");
        }


        private void RefreshQuestList()
        {
            UIFactory.ClearChildren(questListContainer);

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
            questReward.text = $"Base Rewards: ${quest.BonusDollar} + {quest.BonusRep} Rep\n" +
            "Rewards on Total Item Price:\n" +
                $"Dollar Multiplier: {quest.DollarMultiplierMin} - {quest.DollarMultiplierMax}\n" +
                $"Rep Multiplier: {quest.RepMultiplierMin} - {quest.RepMultiplierMax}";
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
            QuestImage = quest.QuestImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Silkroad", "SilkRoadIcon_quest.png");
            var q = S1API.Quests.QuestManager.CreateQuest<QuestDelivery>();
            if (q is QuestDelivery delivery)
            {
                delivery.Data.ProductID = quest.ProductID;
                delivery.Data.RequiredAmount = quest.AmountRequired;
                delivery.Data.DealerName = quest.DealerName;
                delivery.Data.QuestImage = QuestImage;
                delivery.Data.RequiredDrug = quest.RequiredDrug;
                delivery.Data.Reward = quest.BonusDollar;
                delivery.Data.RepReward = quest.BonusRep;
                delivery.Data.Task = quest.Task;

                if (Contacts.GetBuyer(quest.DealerName) is BlackmarketBuyer buyer)
                {
                    buyer.SendCustomMessage("DealStart", quest.ProductID, (int)quest.AmountRequired);
                }
            }

            deliveryStatus.text = "📦 Delivery started!";
        }
    }
}