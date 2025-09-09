using System.Collections.Generic;
using S1API.Internal.Utils;
using S1API.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Empire
{
    public partial class MyApp
    {
        // UI element fields
        private Text questTitle, questTask, questReward, deliveryStatus, acceptLabel, cancelLabel, refreshLabel, manageLabel;
        private Button acceptButton, cancelButton, refreshButton, manageButton;
        private Text statusText;
        private Text managementTabLabel;

        // UI containers and row lists for selection highlighting
        private RectTransform questListContainer;
        private RectTransform buyerListContainer;
        private readonly List<GameObject> questRows = new List<GameObject>();
        private readonly List<GameObject> buyerRows = new List<GameObject>();
        private GameObject managementDetailPanel;

        private GameObject mainLeftPanel;
        private GameObject mainRightPanel;

        // IMPROVEMENT: Dictionary to manage tab buttons for active state highlighting.
        private readonly Dictionary<string, Button> tabButtons = new Dictionary<string, Button>();

        protected override void OnCreatedUI(GameObject container)
        {
            var bg = UIFactory.Panel("MainBG", container.transform, Color.black, fullAnchor: true);

            UIFactory.TopBar("TopBar", bg.transform, "Deals", 0.82f, 75, 75, 0, 35);

            statusText = UIFactory.Text("Status", "", bg.transform, 14);
            statusText.rectTransform.anchorMin = new Vector2(0.7f, 0.85f);
            statusText.rectTransform.anchorMax = new Vector2(0.98f, 0.9f);
            statusText.alignment = TextAnchor.MiddleRight;

            mainLeftPanel = UIFactory.Panel("QuestListPanel", bg.transform, new Color(0.1f, 0.1f, 0.1f), new Vector2(0.02f, 0.05f), new Vector2(0.49f, 0.82f));
            questListContainer = UIFactory.ScrollableVerticalList("QuestListScroll", mainLeftPanel.transform, out _);
            UIFactory.FitContentHeight(questListContainer);

            mainRightPanel = UIFactory.Panel("DetailPanel", bg.transform, new Color(0.12f, 0.12f, 0.12f), new Vector2(0.49f, 0f), new Vector2(0.98f, 0.82f));
            UIFactory.VerticalLayoutOnGO(mainRightPanel, 14, new RectOffset(24, 50, 15, 70));
            questTitle = UIFactory.Text("Title", "", mainRightPanel.transform, 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            questTask = UIFactory.Text("Task", "", mainRightPanel.transform, 18);
            questReward = UIFactory.Text("Reward", "", mainRightPanel.transform, 18);
            deliveryStatus = UIFactory.Text("DeliveryStatus", "", mainRightPanel.transform, 16, TextAnchor.MiddleLeft, FontStyle.Italic);
            deliveryStatus.color = new Color(32 / 255f, 130 / 255f, 246 / 255f);

            var acceptTuple = UIFactory.RoundedButtonWithLabel("AcceptBtn", "No quest selected", mainRightPanel.transform, new Color32(32, 130, 246, 255), 460f, 60f, 22, Color.black);
            acceptButton = acceptTuple.Item2;
            acceptLabel = acceptTuple.Item3;
            ButtonUtils.Disable(acceptButton, acceptLabel, "No quest selected");

            var cancelTuple = UIFactory.RoundedButtonWithLabel("CancelBtn", "Cancel", mainRightPanel.transform, new Color32(220, 53, 69, 255), 460f, 60f, 22, Color.white);
            cancelButton = cancelTuple.Item2;
            cancelLabel = cancelTuple.Item3;
            if (!QuestDelivery.QuestActive)
                ButtonUtils.Disable(cancelButton, cancelLabel, "No quest active");

            var manageTuple = UIFactory.RoundedButtonWithLabel("ManageBtn", "Manage", bg.transform, new Color(0.2f, 0.2f, 0.2f, 1f), 100, 40, 16, Color.white);
            manageButton = manageTuple.Item2;
            manageLabel = manageTuple.Item3;
            ButtonUtils.AddListener(manageButton, () => OpenManageUI(bg));
            var manageRect = manageTuple.Item1.GetComponent<RectTransform>();
            manageRect.anchorMin = new Vector2(0.75f, 0.96f);
            manageRect.anchorMax = new Vector2(0.85f, 1f);
            manageRect.pivot = new Vector2(1f, 1f);
            manageRect.anchoredPosition = new Vector2(-10f, -10f);
            manageRect.sizeDelta = new Vector2(50, 25);

            var refreshTuple = UIFactory.RoundedButtonWithLabel("RefreshBtn", "Contact", bg.transform, new Color(0.2f, 0.2f, 0.2f, 1f), 300, 90, 22, Color.white);
            refreshButton = refreshTuple.Item2;
            refreshLabel = refreshTuple.Item3;
            ButtonUtils.AddListener(refreshButton, RefreshButton);
            var refreshRect = refreshTuple.Item1.GetComponent<RectTransform>();
            refreshRect.anchorMin = new Vector2(0.9f, 0.96f);
            refreshRect.anchorMax = new Vector2(1f, 1f);
            refreshRect.pivot = new Vector2(1f, 1f);
            refreshRect.anchoredPosition = new Vector2(-10f, -10f);
            refreshRect.sizeDelta = new Vector2(50, 25);

            if (Contacts.Buyers == null || Contacts.Buyers.Count == 0)
            {
                InitializeDealers();
            }
            MelonLoader.MelonCoroutines.Start(WaitForBuyerAndInitialize());
        }

        private void HighlightRow(GameObject selectedRow, List<GameObject> allRows)
        {
            Color defaultColor = new Color(0.12f, 0.12f, 0.12f);
            foreach (var row in allRows)
            {
                if (row != null)
                {
                    var image = row.GetComponent<Image>();
                    if (image != null) image.color = defaultColor;
                }
            }
            if (selectedRow != null)
            {
                var image = selectedRow.GetComponent<Image>();
                if (image != null) image.color = Color.black;
            }
        }

        // IMPROVEMENT: Added method to visually highlight the currently selected tab button.
        private void HighlightTabButton(string activeTab)
        {
            Color activeColor = new Color(0.25f, 0.45f, 0.85f); // A distinct color for the active tab
            Color inactiveColor = Color.gray;

            foreach (var entry in tabButtons)
            {
                var image = entry.Value.GetComponent<Image>();
                if (image != null)
                {
                    image.color = (entry.Key == activeTab) ? activeColor : inactiveColor;
                }
            }
        }

        public static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        private System.Collections.IEnumerator BlinkMessage(Text message)
        {
            yield return new WaitForSeconds(2.5f);
            if (message != null && message.gameObject != null)
            {
                Object.Destroy(message.gameObject);
            }
        }
    }
}