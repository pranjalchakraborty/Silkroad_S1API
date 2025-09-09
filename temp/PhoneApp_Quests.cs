using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Internal.Utils;
using S1API.Money;
using S1API.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Empire
{
    public partial class MyApp
    {
        private List<QuestData> quests;
        private static int Index;

        private void LoadQuests()
        {
            quests = new List<QuestData>();
            string currentDay = S1API.GameTime.TimeManager.CurrentDay.ToString();

            foreach (var buyer in Contacts.Buyers.Values)
            {
                if (!buyer.IsInitialized || buyer.DealDays == null || !buyer.DealDays.Contains(currentDay)) continue;

                var saveData = BlackmarketBuyer.GetDealerSaveData(buyer.DealerName);
                if (saveData == null || saveData.ShippingTier < 0 || saveData.ShippingTier >= buyer.Shippings.Count) continue;

                var drugType = saveData.UnlockedDrugs.Select(d => d.Type).Distinct().OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                if (drugType != null)
                {
                    GenerateQuest(buyer, saveData, drugType);
                }
            }
            RefreshQuestList();
        }

        int RoundToHalfMSD(int value)
        {
            if (value == 0) return 0;
            int digits = (int)Math.Floor(Math.Log10(value)) + 1;
            int keep = (digits + 1) / 2;
            int roundFactor = (int)Math.Pow(10, digits - keep);
            return ((value + roundFactor - 1) / roundFactor) * roundFactor;
        }

        private void GenerateQuest(BlackmarketBuyer buyer, DealerSaveData dealerSaveData, string drugType)
        {
            var shipping = buyer.Shippings[dealerSaveData.ShippingTier];
            int minAmount = shipping.MinAmount;
            int maxAmount = shipping.MaxAmount;
            if (buyer.RepLogBase > 1)
            {
                double logResult = Math.Log((double)buyer._DealerData.Reputation + 1, (double)buyer.RepLogBase);
                if (logResult < 4) logResult = 0;
                else logResult = logResult - 4;
                minAmount = (int)(minAmount * (1 + logResult));
                maxAmount = (int)(maxAmount * (1 + logResult));
            }
            int steps = (maxAmount - minAmount) / shipping.StepAmount;
            int randomStep = RandomUtils.RangeInt(0, steps + 1);
            int amount = minAmount + randomStep * shipping.StepAmount;

            var unlockedDrugs = dealerSaveData.UnlockedDrugs.Where(d => d.Type == drugType).ToList();
            if (!unlockedDrugs.Any()) return;

            var randomDrug = unlockedDrugs[RandomUtils.RangeInt(0, unlockedDrugs.Count)];
            if (!randomDrug.Qualities.Any()) return;

            var randomQuality = randomDrug.Qualities[RandomUtils.RangeInt(0, randomDrug.Qualities.Count)];

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

            var qualityKey = randomQuality.Type.Trim().ToLowerInvariant();
            var qualityMult = 0f;
            if (JSONDeserializer.QualitiesDollarMult.ContainsKey(qualityKey))
            {
                quality = qualityKey;
                qualityMult = randomQuality.DollarMult + JSONDeserializer.QualitiesDollarMult[qualityKey];
            }

            aggregateDollarMultMin = 1 + qualityMult;
            aggregateDollarMultMax = 1 + qualityMult;

            var tempMult11 = 1f;
            var tempMult21 = 1f;
            var randomNum1 = UnityEngine.Random.Range(JSONDeserializer.RandomNumberRanges[0], JSONDeserializer.RandomNumberRanges[1]);

            foreach (var effect in randomDrug.Effects)
            {
                var roll = UnityEngine.Random.Range(0f, 1f);
                bool isNecessary = effect.Probability > 1f && roll < (effect.Probability - 1f);
                bool isOptional = effect.Probability <= 1f && roll < effect.Probability;

                if (isNecessary || isOptional)
                {
                    string effectNameToAdd = effect.Name;
                    if (effect.Name == "Random")
                    {
                        effectNameToAdd = JSONDeserializer.dealerData.EffectsName
                            .Where(name => name != "Random" && !necessaryEffects.Contains(name) && !optionalEffects.Contains(name))
                            .OrderBy(_ => Guid.NewGuid())
                            .FirstOrDefault();
                    }
                    if (string.IsNullOrEmpty(effectNameToAdd)) continue;

                    var effectKey = effectNameToAdd.Trim().ToLowerInvariant();
                    float effectDollarMult = effect.DollarMult;
                    if (JSONDeserializer.EffectsDollarMult.ContainsKey(effectKey))
                    {
                        effectDollarMult += JSONDeserializer.EffectsDollarMult[effectKey];
                    }

                    if (isNecessary)
                    {
                        necessaryEffects.Add(effectNameToAdd);
                        necessaryEffectMult.Add(effectDollarMult * randomNum1);
                        tempMult11 += effectDollarMult * randomNum1;
                        tempMult21 += effectDollarMult * randomNum1;
                    }
                    else
                    {
                        optionalEffects.Add(effectNameToAdd);
                        optionalEffectMult.Add(effectDollarMult * randomNum1);
                        tempMult21 += effectDollarMult * randomNum1;
                    }
                }
            }

            aggregateDollarMultMin *= tempMult11;
            aggregateDollarMultMax *= tempMult21;

            string effectDesc = "";
            if (necessaryEffects.Count > 0) effectDesc += $"Required: {string.Join(", ", necessaryEffects)}";
            if (optionalEffects.Count > 0) effectDesc += (effectDesc.Length > 0 ? "; " : "") + $"Optional: {string.Join(", ", optionalEffects)}";
            if (string.IsNullOrEmpty(effectDesc)) effectDesc = "Effects: none";

            var randomNum2 = UnityEngine.Random.Range(JSONDeserializer.RandomNumberRanges[2], JSONDeserializer.RandomNumberRanges[3]);
            var randomNum3 = UnityEngine.Random.Range(JSONDeserializer.RandomNumberRanges[4], JSONDeserializer.RandomNumberRanges[5]);
            var randomNum4 = UnityEngine.Random.Range(JSONDeserializer.RandomNumberRanges[6], JSONDeserializer.RandomNumberRanges[7]);

            aggregateDollarMultMin *= dealTimesMult * randomNum4;
            aggregateDollarMultMax *= dealTimesMult * randomNum4;

            var quest = new QuestData
            {
                Title = $"{buyer.DealerName}: {drugType}",
                Task = $"Deliver {amount}x {quality} {drugType}\n{effectDesc}",
                ProductID = drugType,
                AmountRequired = (uint)amount,
                DealerName = buyer.DealerName,
                QuestImage = Path.Combine(MelonEnvironment.ModsDirectory, "Empire", buyer.DealerImage ?? "EmpireIcon_quest.png"),
                BaseDollar = RoundToHalfMSD((int)(randomDrug.BaseDollar * amount / randomNum4)),
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
        }

        private void RefreshButton()
        {
            int cost = Contacts.Buyers.Values.Where(b => b.IsInitialized && b.DealDays.Contains(S1API.GameTime.TimeManager.CurrentDay.ToString())).Sum(b => b.RefreshCost);
            if (Money.GetCashBalance() < cost)
            {
                deliveryStatus.text = $"Not enough cash to refresh (${cost})";
                return;
            }
            Money.ChangeCashBalance(-cost);
            LoadQuests();
        }

        private void RefreshQuestList()
        {
            ClearChildren(questListContainer);
            questRows.Clear();
            Index = 0;
            foreach (var quest in quests)
            {
                var row = UIFactory.CreateQuestRow(quest.Title, questListContainer, out var iconPanel, out var textPanel);
                questRows.Add(row);
                quest.Index = row.transform.GetSiblingIndex();

                UIFactory.SetIcon(ImageUtils.LoadImage(quest.QuestImage), iconPanel.transform);
                var icon = iconPanel.transform.GetComponentInChildren<Image>();
                if (icon != null) icon.GetComponent<RectTransform>().sizeDelta = new Vector2(128, 128);

                ButtonUtils.AddListener(row.GetComponent<Button>(), () => OnSelectQuest(quest));
                UIFactory.CreateTextBlock(textPanel.transform, quest.Title, quest.Task, false);
            }
        }

        private void OnSelectQuest(QuestData quest)
        {
            var buyer = Contacts.GetBuyer(quest.DealerName);
            var dialogue = buyer.SendCustomMessage("DealStart", quest.ProductID, (int)quest.AmountRequired, quest.Quality, quest.NecessaryEffects, quest.OptionalEffects, 0, true);
            questTitle.text = quest.Title;
            questTask.text = dialogue;

            questReward.text =
                $"<b><color=#FFD700>Rewards:</color></b> <color=#00FF00>${quest.BaseDollar:N0} / {(quest.AmountRequired > 0 ? (quest.BaseDollar / quest.AmountRequired) : 0):N0} per piece</color> + <i>Price x</i> (<color=#00FFFF>{quest.DollarMultiplierMin}</color> - <color=#00FFFF>{quest.DollarMultiplierMax}</color>)\n" +
                $"<b><color=#FFD700>Reputation:</color></b> <color=#00FF00>{quest.BaseRep}</color> + Rewards x <color=#00FFFF>{Math.Round(quest.RepMult, 4)}</color>\n" +
                $"<b><color=#FFD700>XP:</color></b> <color=#00FF00>{quest.BaseXp}</color> + Rewards x <color=#00FFFF>{Math.Round(quest.XpMult, 4)}</color>\n\n" +
                $"<b><color=#FF6347>Deal Expiry:</color></b> <color=#FFA500>{quest.DealTime} Days</color>\n" +
                $"<b><color=#FF6347>Failure Penalties:</color></b> <color=#FF0000>${quest.Penalties[0]:N0}</color> + <color=#FF4500>{quest.Penalties[1]} Rep</color>";

            deliveryStatus.text = "";

            if (!QuestDelivery.QuestActive)
            {
                ButtonUtils.Enable(acceptButton, acceptLabel, "Accept Delivery");
                ButtonUtils.ClearListeners(acceptButton);
                ButtonUtils.AddListener(acceptButton, () => AcceptQuest(quest));
            }
            else
            {
                ButtonUtils.Disable(acceptButton, acceptLabel, "In Progress");
            }

            if (QuestDelivery.QuestActive) ButtonUtils.Enable(cancelButton, cancelLabel, "Cancel");
            ButtonUtils.ClearListeners(cancelButton);
            ButtonUtils.AddListener(cancelButton, () => CancelCurrentQuest(quest));

            HighlightRow(questRows.ElementAtOrDefault(quest.Index), questRows);
        }

        private void AcceptQuest(QuestData quest)
        {
            if (QuestDelivery.QuestActive)
            {
                deliveryStatus.text = "Finish your current job first!";
                return;
            }

            var delivery = S1API.Quests.QuestManager.CreateQuest<QuestDelivery>() as QuestDelivery;
            if (delivery != null)
            {
                delivery.Data.ProductID = quest.ProductID;
                delivery.Data.RequiredAmount = quest.AmountRequired;
                delivery.Data.DealerName = quest.DealerName;
                delivery.Data.QuestImage = quest.QuestImage;
                delivery.Data.Task = quest.Task;
                delivery.Data.Reward = quest.BaseDollar;
                delivery.Data.RepReward = quest.BaseRep;
                delivery.Data.XpReward = quest.BaseXp;
                delivery.Data.RepMult = quest.RepMult;
                delivery.Data.XpMult = quest.XpMult;
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

                var buyer = Contacts.GetBuyer(quest.DealerName);
                buyer.SendCustomMessage("Accept");

                quests.Remove(quest);
                RefreshQuestList();
                ButtonUtils.Disable(acceptButton, acceptLabel, "In Progress");
            }
            else
            {
                MelonLogger.Error("Failed to create and cast QuestDelivery instance.");
            }
        }

        private void CancelCurrentQuest(QuestData quest)
        {
            if (QuestDelivery.Active == null) return;
            QuestDelivery.Active.ForceCancel();
            deliveryStatus.text = "Delivery canceled.";
            ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");
            ButtonUtils.Enable(acceptButton, acceptLabel, "Accept Delivery");
            RefreshQuestList();
        }

        public void OnQuestComplete()
        {
            ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");
        }
    }
}