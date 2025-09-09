using System;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Console;
using S1API.GameTime;
using S1API.Internal.Utils;
using S1API.Money;
using S1API.UI;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Empire
{
    public partial class MyApp
    {
        private BlackmarketBuyer selectedBuyer;

        private void OpenManageUI(GameObject bg)
        {
            mainLeftPanel.SetActive(false);
            mainRightPanel.SetActive(false);

            var managementPanel = UIFactory.Panel("ManagementPanel", bg.transform, new Color(0.1f, 0.1f, 0.1f, 1f), fullAnchor: true);
            managementPanel.transform.SetAsLastSibling();

            var topBar = UIFactory.Panel("ManageTopBar", managementPanel.transform, new Color(0.05f, 0.05f, 0.05f, 1f));
            var topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0, 0.85f);
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.offsetMin = Vector2.zero;
            topBarRect.offsetMax = Vector2.zero;
            UIFactory.HorizontalLayoutOnGO(topBar, 10, 10, 10, 10, 10, TextAnchor.MiddleLeft);

            tabButtons.Clear();
            tabButtons["Reputation"] = UIFactory.RoundedButtonWithLabel("RepButton", "Reputation", topBar.transform, Color.gray, 120, 40, 16, Color.white).Item2;
            tabButtons["Product"] = UIFactory.RoundedButtonWithLabel("ProdButton", "Product", topBar.transform, Color.gray, 120, 40, 16, Color.white).Item2;
            tabButtons["Shipping"] = UIFactory.RoundedButtonWithLabel("ShipButton", "Shipping", topBar.transform, Color.gray, 120, 40, 16, Color.white).Item2;
            tabButtons["Gift"] = UIFactory.RoundedButtonWithLabel("GiftsButton", "Gift", topBar.transform, Color.gray, 120, 40, 16, Color.white).Item2;
            tabButtons["Debt"] = UIFactory.RoundedButtonWithLabel("DebtButton", "Debt", topBar.transform, Color.gray, 120, 40, 16, Color.white).Item2;

            foreach (var tab in tabButtons)
            {
                ButtonUtils.AddListener(tab.Value, () => UpdateBuyerDetails(tab.Key));
            }

            var spacer = topBar.AddComponent<LayoutElement>();
            spacer.flexibleWidth = 1;

            managementTabLabel = UIFactory.Text("ManagementTabLabel", "", topBar.transform, 20, TextAnchor.MiddleCenter, FontStyle.Bold);
            var closeTuple = UIFactory.RoundedButtonWithLabel("CloseButton", "X", topBar.transform, new Color32(235, 53, 56, 255), 50, 40, 16, Color.white);
            ButtonUtils.AddListener(closeTuple.Item2, () => {
                Object.Destroy(managementPanel);
                mainLeftPanel.SetActive(true);
                mainRightPanel.SetActive(true);
            });

            var contentPanel = UIFactory.Panel("ManageContent", managementPanel.transform, new Color(0.12f, 0.12f, 0.12f, 1f));
            var contentRect = contentPanel.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 0.85f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // BUG FIX: The right panel MUST be created and assigned BEFORE the left panel is populated.
            // This is the definitive fix for the NullReferenceException.

            // 1. Create the right panel that will hold the details.
            var rightPanel = UIFactory.Panel("DetailPanel", contentPanel.transform, new Color(0.12f, 0.12f, 0.12f, 1f));
            var rightRect = rightPanel.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.4f, 0); // Anchor to right 60%
            rightRect.anchorMax = new Vector2(1, 1);
            rightRect.offsetMin = Vector2.zero;
            rightRect.offsetMax = Vector2.zero;
            rightPanel.AddComponent<RectMask2D>();
            managementDetailPanel = rightPanel; // 2. Assign the panel to the class variable.

            // 3. Create the left panel.
            var leftPanel = UIFactory.Panel("BuyerListPanel", contentPanel.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
            var leftRect = leftPanel.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0, 0);
            leftRect.anchorMax = new Vector2(0.4f, 1); // Anchor to left 40%
            leftRect.offsetMin = Vector2.zero;
            leftRect.offsetMax = Vector2.zero;

            buyerListContainer = UIFactory.ScrollableVerticalList("BuyerListScroll", leftPanel.transform, out _);
            UIFactory.FitContentHeight(buyerListContainer);

            // 4. Now it is safe to populate the list, which will trigger click events that require managementDetailPanel.
            PopulateBuyerList(buyerListContainer.transform);

            // 5. Auto-select the first buyer to ensure the UI is populated on open.
            if (buyerListContainer.childCount > 0)
            {
                var firstButton = buyerListContainer.GetChild(0).GetComponent<Button>();
                firstButton?.onClick.Invoke();
            }
        }

        private void PopulateBuyerList(Transform container)
        {
            ClearChildren(container);
            buyerRows.Clear();

            foreach (var buyer in Contacts.Buyers.Values.Where(b => b.IsInitialized).OrderBy(b => b.DealerName))
            {
                var row = UIFactory.CreateQuestRow(buyer.DealerName, container, out var iconPanel, out var textPanel);
                buyerRows.Add(row);
                UIFactory.SetIcon(ImageUtils.LoadImage(Path.Combine(MelonEnvironment.ModsDirectory, "Empire", buyer.DealerImage ?? "EmpireIcon_quest.png")), iconPanel.transform);

                ButtonUtils.AddListener(row.GetComponent<Button>(), () =>
                {
                    selectedBuyer = buyer;
                    UpdateBuyerDetails(managementTabLabel?.text ?? "Reputation");
                    HighlightRow(row, buyerRows);
                });
                UIFactory.CreateTextBlock(textPanel.transform, buyer.DealerName, "", false);
            }
        }

        private void UpdateBuyerDetails(string tab)
        {
            if (managementDetailPanel == null || selectedBuyer == null) return;

            ClearChildren(managementDetailPanel.transform);
            UIFactory.VerticalLayoutOnGO(managementDetailPanel, 14, new RectOffset(24, 50, 15, 70));

            managementTabLabel.text = tab;
            HighlightTabButton(tab);
            string content;

            if (tab == "Reputation")
            {
                var imagePath = selectedBuyer.DealerImage ?? Path.Combine(MelonEnvironment.ModsDirectory, "Empire", "EmpireIcon_quest.png");
                UIFactory.SetIcon(ImageUtils.LoadImage(imagePath), managementDetailPanel.transform);
                var icon = managementDetailPanel.transform.GetComponentInChildren<Image>();
                if (icon != null) icon.GetComponent<RectTransform>().sizeDelta = new Vector2(127, 127);

                content = $"<b>Reputation:</b> {selectedBuyer._DealerData.Reputation}";

                if (selectedBuyer.DealDays != null && selectedBuyer.DealDays.Any())
                {
                    content += $"\n\n<b><color=#FFA500>Deal Days:</color></b> <color=#FFFFFF>{string.Join(", ", selectedBuyer.DealDays)}</color>";
                }

                var pendingBuyers = Contacts.Buyers.Values
                    .Where(b => !b.IsInitialized && b.UnlockRequirements != null && b.UnlockRequirements.Any(r => r.Name == selectedBuyer.DealerName && r.MinRep > selectedBuyer._DealerData.Reputation))
                    .ToList();

                if (pendingBuyers.Any())
                {
                    content += "\n\n<b>Pending Unlocks:</b>\n";
                    foreach (var buyer in pendingBuyers)
                    {
                        var req = buyer.UnlockRequirements.First(r => r.Name == selectedBuyer.DealerName);
                        content += $"• {buyer.DealerName}: Requires Rep {req.MinRep}\n";
                    }
                }
                UIFactory.Text("DetailText", content, managementDetailPanel.transform, 18);
            }
            else if (tab == "Product")
            {
                UIFactory.Text("ProductText", selectedBuyer.GetDrugUnlockInfo(), managementDetailPanel.transform, 18);
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
                                      $"   • <i>Cost:</i> <color=#00FFFF>${currentShip.Cost:N0}</color>\n" +
                                      $"   • <i>Unlock Rep:</i> <color=#00FF00>{currentShip.UnlockRep}</color>\n" +
                                      $"   • <i>Amounts:</i> <color=#FFFF00>{currentShip.MinAmount} - {currentShip.MaxAmount}</color>\n" +
                                      $"   • <i>Deal Modifier:</i> <color=#FFA500>{string.Join(", ", currentShip.DealModifier)}</color>\n";
                }

                if (selectedBuyer.Shippings != null && currentTier + 1 < selectedBuyer.Shippings.Count)
                {
                    var nextShip = selectedBuyer.Shippings[currentTier + 1];
                    nextShipping = $"<b><color=#FF6347>Next Tier ({currentTier + 1})</color></b>\n" +
                                   $"   • <i>Name:</i> <color=#FFFFFF>{nextShip.Name}</color>\n" +
                                   $"   • <i>Cost:</i> <color=#00FFFF>${nextShip.Cost:N0}</color>\n" +
                                   $"   • <i>Unlock Rep:</i> <color=#00FF00>{nextShip.UnlockRep}</color>\n" +
                                   $"   • <i>Amounts:</i> <color=#FFFF00>{nextShip.MinAmount} - {nextShip.MaxAmount}</color>\n" +
                                   $"   • <i>Deal Modifier:</i> <color=#FFA500>{string.Join(", ", nextShip.DealModifier)}</color>\n";

                    UIFactory.Text("ShippingDetailText", currentShipping + "\n" + nextShipping, managementDetailPanel.transform, 18);

                    var upgradeBtn = UIFactory.RoundedButtonWithLabel("UpgradeShipping", "Upgrade Shipping", managementDetailPanel.transform, new Color32(0, 123, 255, 255), 240, 70, 22, Color.white).Item2;
                    ButtonUtils.AddListener(upgradeBtn, () => {
                        if (Money.GetCashBalance() >= nextShip.Cost && selectedBuyer._DealerData.Reputation >= nextShip.UnlockRep)
                        {
                            Money.ChangeCashBalance(-nextShip.Cost);
                            selectedBuyer.UpgradeShipping();
                            UpdateBuyerDetails("Shipping");
                        }
                        else
                        {
                            var errorText = UIFactory.Text("UpgradeError", "<color=red>Cannot afford upgrade or rep too low.</color>", managementDetailPanel.transform, 18);
                            MelonCoroutines.Start(BlinkMessage(errorText));
                        }
                    });
                }
                else
                {
                    nextShipping = "<b><color=#FF6347>Next Tier:</color></b> <color=#FFFFFF>Maximum tier unlocked.</color>";
                    UIFactory.Text("ShippingDetailText", currentShipping + "\n" + nextShipping, managementDetailPanel.transform, 18);
                }
            }
            else if (tab == "Gift")
            {
                UIFactory.Text("GiftInfoText", $"Give a gift to {selectedBuyer.DealerName} to improve relations.\n\nCost: ${selectedBuyer.Gift.Cost:N0}\nRep Gain: +{selectedBuyer.Gift.Rep}", managementDetailPanel.transform, 18);
                var giftTuple = UIFactory.RoundedButtonWithLabel("GiveGift", "Give Gift", managementDetailPanel.transform, new Color32(40, 167, 69, 255), 240, 70, 22, Color.white);
                ButtonUtils.ClearListeners(giftTuple.Item2);
                ButtonUtils.AddListener(giftTuple.Item2, () => {
                    int cost = selectedBuyer.Gift.Cost;
                    if (Money.GetCashBalance() >= cost)
                    {
                        ConsoleHelper.RunCashCommand(-cost);
                        selectedBuyer.GiveReputation(selectedBuyer.Gift.Rep);
                        var successText = UIFactory.Text("GiftSuccess", $"<color=green>Gift given! Reputation increased by {selectedBuyer.Gift.Rep}.</color>", managementDetailPanel.transform, 18);
                        MelonCoroutines.Start(BlinkMessage(successText));
                        Contacts.Update();
                    }
                    else
                    {
                        var errorText = UIFactory.Text("GiftError", "<color=red>Not enough cash for a gift.</color>", managementDetailPanel.transform, 18);
                        MelonCoroutines.Start(BlinkMessage(errorText));
                    }
                });

                var rewardManager = selectedBuyer.RewardManager;
                string rewardType = rewardManager.GetRewardType();
                if (selectedBuyer.Reward != null && selectedBuyer.Reward.Args != null && selectedBuyer.Reward.Args.Any())
                {
                    rewardType += $" - {string.Join(" ", selectedBuyer.Reward.Args)} - Reward will be given after 10 secs";
                }
                else
                {
                    rewardType = "No reward available";
                }

                UIFactory.Text("RewardTypeText", $"\n<b>Claimable Reward:</b>\n{rewardType}", managementDetailPanel.transform, 18);
                var rewardBtn = UIFactory.RoundedButtonWithLabel("ClaimReward", "Claim Reward", managementDetailPanel.transform, new Color32(0, 123, 255, 255), 240, 70, 22, Color.white).Item2;
                ButtonUtils.ClearListeners(rewardBtn);
                ButtonUtils.AddListener(rewardBtn, () => {
                    if (!rewardManager.isRewardAvailable)
                    {
                        UIFactory.Text("RewardResult", "<color=red>Reward not available today.</color>", managementDetailPanel.transform, 18);
                        return;
                    }
                    if (selectedBuyer.Reward == null || selectedBuyer.Reward.Args == null || !selectedBuyer.Reward.Args.Any() || rewardType == "No reward available")
                    {
                        UIFactory.Text("RewardResult", "<color=red>No reward available from this contact.</color>", managementDetailPanel.transform, 18);
                        return;
                    }
                    if (selectedBuyer.Reward?.unlockRep > selectedBuyer._DealerData.Reputation)
                    {
                        UIFactory.Text("RewardResult", $"<color=red>Reputation too low. Requires {selectedBuyer.Reward.unlockRep}.</color>", managementDetailPanel.transform, 18);
                        return;
                    }
                    rewardManager.GiveReward();
                    UIFactory.Text("RewardResult", "<color=green>Reward will be given in 10 secs!</color>", managementDetailPanel.transform, 18);
                });
            }
            else if (tab == "Debt")
            {
                if (selectedBuyer.Debt == null || selectedBuyer._DealerData.DebtRemaining <= 0.01)
                {
                    UIFactory.Text("NoDebtText", "<b>No outstanding debt with this contact.</b>", managementDetailPanel.transform, 18);
                    return;
                }

                float weeklyPayment = selectedBuyer.Debt.DayMultiple * (float)Math.Pow(DebtManager.GetNearestWeek(TimeManager.ElapsedDays), selectedBuyer.Debt.DayExponent);
                weeklyPayment = Math.Min(weeklyPayment, selectedBuyer._DealerData.DebtRemaining);

                string debtInfo = $"<b>Total Debt:</b> <color=#FF4500>${selectedBuyer._DealerData.DebtRemaining:F2}</color>\n" +
                                  $"<b>Next Weekly Payment:</b> <color=#FFA500>${weeklyPayment:F2}</color>\n" +
                                  $"<b>Interest Rate:</b> {selectedBuyer.Debt.InterestRate * 100:F1}%\n" +
                                  $"<b>Paid This Week:</b> <color=#00FF00>${selectedBuyer._DealerData.DebtPaidThisWeek:F2}</color>";
                UIFactory.Text("DebtInfoText", debtInfo, managementDetailPanel.transform, 18);

                float tenPercent = selectedBuyer._DealerData.DebtRemaining * 0.10f;
                float amountToPay = Math.Max(1000f, tenPercent);
                amountToPay = Math.Min(amountToPay, selectedBuyer._DealerData.DebtRemaining);

                var payTuple = UIFactory.RoundedButtonWithLabel("PayDebtButton", $"Pay ${amountToPay:F0}", managementDetailPanel.transform, new Color32(0, 123, 255, 255), 240, 70, 22, Color.white);
                ButtonUtils.AddListener(payTuple.Item2, () => {
                    if (Money.GetCashBalance() >= amountToPay)
                    {
                        Money.ChangeCashBalance(-amountToPay);
                        selectedBuyer._DealerData.DebtRemaining -= amountToPay;
                        UpdateBuyerDetails("Debt");
                    }
                    else
                    {
                        var error = managementDetailPanel.transform.Find("DebtErrorText");
                        if (error != null) Object.Destroy(error.gameObject);
                        var errorText = UIFactory.Text("DebtErrorText", "<color=red>Not enough cash.</color>", managementDetailPanel.transform, 18);
                        MelonCoroutines.Start(BlinkMessage(errorText));
                    }
                });
            }
        }
    }
}