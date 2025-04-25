#if IL2CPP
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#else
using UnityEngine;
using UnityEngine.UI;
#endif

using System;
using UnityEngine.Events;
using System.Collections.Generic;
using MelonLoader;
using Object = UnityEngine.Object;

namespace S1API.UI
{
    /// <summary>
    /// Static utility class for dynamically generating and managing UI elements within Unity applications.
    /// </summary>
    /// <remarks>
    /// Contains methods for creating reusable and customizable UI components, such as panels, buttons, text elements, layouts,
    /// and more. Designed to facilitate rapid development and organization of UI hierarchies, with options
    /// for styling and behavior configuration.
    /// </remarks>
    public static class UIFactory
    {
        /// Creates a UI panel with a background color and optional anchoring.
        /// <param name="name">The name of the GameObject representing the panel.</param>
        /// <param name="parent">The transform to which the panel will be parented.</param>
        /// <param name="bgColor">The background color of the panel.</param>
        /// <param name="anchorMin">The minimum anchor point of the RectTransform. Defaults to (0.5, 0.5) if not specified.</param>
        /// <param name="anchorMax">The maximum anchor point of the RectTransform. Defaults to (0.5, 0.5) if not specified.</param>
        /// <param name="fullAnchor">Whether to stretch the panel across the entire parent RectTransform. Overrides anchorMin and anchorMax if true.</param>
        /// <returns>The GameObject representing the created UI panel.</returns>
        public static GameObject Panel(string name, Transform parent, Color bgColor, Vector2? anchorMin = null,
            Vector2? anchorMax = null, bool fullAnchor = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();

            if (fullAnchor)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
                rt.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
            }

            var img = go.AddComponent<Image>();
            img.color = bgColor;
            return go;
        }

        /// Creates a Text UI element with specified properties.
        /// <param name="name">The name of the GameObject to create for the text element.</param>
        /// <param name="content">The content of the text to display.</param>
        /// <param name="parent">The Transform to which the created text GameObject will be assigned.</param>
        /// <param name="fontSize">The font size of the text. Defaults to 14.</param>
        /// <param name="anchor">The alignment of the text within its RectTransform. Defaults to `TextAnchor.UpperLeft`.</param>
        /// <param name="style">The font style of the text. Defaults to `FontStyle.Normal`.</param>
        /// <returns>The created Text component with the specified properties applied.</returns>
        public static Text Text(string name, string content, Transform parent, int fontSize = 14,
            TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();

            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.fontStyle = style;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        /// Creates a scrollable vertical list UI component with a configured child hierarchy, allowing vertical scrolling of dynamically added items.
        /// <param name="name">The name of the scrollable list GameObject.</param>
        /// <param name="parent">The parent transform where the scrollable list will be added.</param>
        /// <param name="scrollRect">Outputs the ScrollRect component associated with the created scrollable list.</param>
        /// <returns>Returns the RectTransform of the "Content" GameObject, allowing items to be added to the scrollable list.</returns>
        public static RectTransform ScrollableVerticalList(string name, Transform parent, out ScrollRect scrollRect)
        {
            var scrollGO = new GameObject(name);
            scrollGO.transform.SetParent(parent, false);
            var scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;

            scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGO.transform, false);
            var viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.05f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = viewportRT;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;
            return contentRT;
        }

        /// Adjusts the height of the content in the RectTransform to fit its preferred size.
        /// Ensures the vertical size of the content adapts to its children's preferred layout.
        /// Adds a ContentSizeFitter component if one is not already present on the specified content.
        /// <param name="content">The RectTransform whose height should be adjusted to fit its content.</param>
        public static void FitContentHeight(RectTransform content)
        {
            var fitter = content.gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// Creates a button with a label and specified dimensions inside a parent UI element.
        /// <param name="name">The name of the button GameObject.</param>
        /// <param name="label">The text to display on the button.</param>
        /// <param name="parent">The Transform to which the button will be attached.</param>
        /// <param name="bgColor">The background color of the button.</param>
        /// <param name="Width">The width of the button.</param>
        /// <param name="Height">The height of the button.</param>
        /// <returns>A tuple containing the button's GameObject, Button component, and Text component.</returns>
        public static (GameObject, Button, Text) ButtonWithLabel(string name, string label, Transform parent,
            Color bgColor, float Width, float Height)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(Height, Width);

            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var txt = textGO.AddComponent<Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return (go, btn, txt);
        }

        /// <summary>
        /// Sets an icon as a child of the specified parent transform with the given sprite.
        /// </summary>
        /// <param name="sprite">The sprite to be used as the icon.</param>
        /// <param name="parent">The transform that will act as the parent of the icon.</param>
        public static void SetIcon(Sprite sprite, Transform parent)
        {
            var icon = new GameObject("Icon");
            icon.transform.SetParent(parent, false);

            var rt = icon.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = icon.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
        }

        /// Creates a text block consisting of a title, subtitle, and an optional completed status label.
        /// <param name="parent">The parent transform where the text block will be added.</param>
        /// <param name="title">The title text of the text block, displayed in bold.</param>
        /// <param name="subtitle">The subtitle text of the text block, displayed below the title.</param>
        /// <param name="isCompleted">A boolean indicating whether the text block represents a completed state. If true, an additional label indicating "Already Delivered" will be added.</param>
        public static void CreateTextBlock(Transform parent, string title, string subtitle, bool isCompleted)
        {
            Text(parent.name + "Title", title, parent, 16, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text(parent.name + "Subtitle", subtitle, parent, 14, TextAnchor.UpperLeft);
            if (isCompleted)
                Text("CompletedLabel", "<color=#888888><i>Already Delivered</i></color>", parent, 12,
                    TextAnchor.UpperLeft);
        }

        /// Adds a button component to the specified game object, sets its target graphic, and configures its interaction settings.
        /// <param name="go">The game object to which the button component is added.</param>
        /// <param name="clickHandler">The UnityAction to invoke when the button is clicked.</param>
        /// <param name="enabled">Determines whether the button is interactable.</param>
        public static void CreateRowButton(GameObject go, UnityAction clickHandler, bool enabled)
        {
            var btn = go.AddComponent<Button>();
            var img = go.GetComponent<Image>();
            btn.targetGraphic = img;
            btn.interactable = enabled;

            btn.onClick.AddListener(clickHandler);
        }

        /// Clears all child objects of the specified parent transform.
        /// <param name="parent">The transform whose child objects will be destroyed.</param>
        public static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
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

            }
            catch (System.Exception e)
            {
                return;
            }
        }

        /// Configures a GameObject to use a VerticalLayoutGroup with specified spacing and padding.
        /// <param name="go">The GameObject to which a VerticalLayoutGroup will be added or configured.</param>
        /// <param name="spacing">The spacing between child objects within the VerticalLayoutGroup. Default is 10.</param>
        /// <param name="padding">The padding around the edges of the VerticalLayoutGroup. If null, a default RectOffset of (10, 10, 10, 10) will be used.</param>
        public static void VerticalLayoutOnGO(GameObject go, int spacing = 10, RectOffset padding = null)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(10, 10, 10, 10);
        }

        /// Creates a quest row GameObject with a specific layout, including an icon panel and text panel.
        /// <param name="name">The name for the row GameObject.</param>
        /// <param name="parent">The parent Transform to attach the row GameObject to.</param>
        /// <param name="iconPanel">An output parameter that receives the generated icon panel GameObject.</param>
        /// <param name="textPanel">An output parameter that receives the generated text panel GameObject.</param>
        /// <returns>The newly created quest row GameObject.</returns>
        public static GameObject CreateQuestRow(string name, Transform parent, out GameObject iconPanel,
            out GameObject textPanel)
        {
            // Create the main row object
            var row = new GameObject("Row_" + name);
            row.transform.SetParent(parent, false);
            var rowRT = row.AddComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0f, 90f); // Let layout handle width
            row.AddComponent<LayoutElement>().minHeight = 50f;
            row.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.2f); // or Image line separator below
            
            
            var line = UIFactory.Panel("Separator", row.transform, new Color(1,1,1,0.05f));
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 1f);

            // Add background + target graphic
            var bg = row.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.12f);

            var button = row.AddComponent<Button>();
            button.targetGraphic = bg;

            // Layout group
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.padding = new RectOffset(75, 10, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.minHeight = 90f;
            rowLE.flexibleWidth = 1;

            // Icon panel
            iconPanel = Panel("IconPanel", row.transform, new Color(0.12f, 0.12f, 0.12f));
            var iconRT = iconPanel.GetComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(80f, 80f);
            var iconLE = iconPanel.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 80f;
            iconLE.preferredHeight = 80f;

            // Text panel
            textPanel = Panel("TextPanel", row.transform, Color.clear);
            VerticalLayoutOnGO(textPanel, spacing: 2);
            var textLE = textPanel.AddComponent<LayoutElement>();
            textLE.minWidth = 200f;
            textLE.flexibleWidth = 1;

            return row;
        }

        /// Creates a top bar UI element with a title and an optional button.
        /// <param name="name">The name of the GameObject representing the top bar.</param>
        /// <param name="parent">The transform to which the top bar will be parented.</param>
        /// <param name="title">The text content for the title displayed in the top bar.</param>
        /// <param name="buttonWidth">The width of the button, if displayed.</param>
        /// <param name="buttonHeight">The height of the button, if displayed.</param>
        /// <param name="onRightButtonClick">An optional action to be invoked when the button is clicked. If null, the button will not be created.</param>
        /// <param name="rightButtonText">The text to display on the optional button. Defaults to "Action" if not specified.</param>
        /// <returns>The created GameObject representing the top bar.</returns>
        public static GameObject TopBar(string name, Transform parent, string title,float buttonWidth,float buttonHeight,float topbarSize,int rectleft,int rectright,int recttop,int rectbottom,
    Action onRightButtonClick = null,
    string rightButtonText = "Action")
{
    var topBar = Panel(name, parent, new Color(0.15f, 0.15f, 0.15f),
        new Vector2(0f, topbarSize), new Vector2(1f, 1f));

    var layout = topBar.AddComponent<HorizontalLayoutGroup>();
    layout.padding = new RectOffset(rectleft,rectright,recttop,rectbottom);;
    layout.spacing = 20;
    layout.childAlignment = TextAnchor.MiddleCenter;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = true;

    // Title
    var titleText = Text("TopBarTitle", title, topBar.transform, 26, TextAnchor.MiddleLeft, FontStyle.Bold);
    var titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
    titleLayout.minWidth = 300;
    titleLayout.flexibleWidth = 1;

    // Button (if any)
    if (onRightButtonClick != null)
    {
        var (btnGO, btn, label) = ButtonWithLabel("TopBarButton", rightButtonText, topBar.transform, new Color(0.25f, 0.5f, 1f), buttonWidth, buttonHeight);
        ButtonUtils.AddListener(btn, onRightButtonClick);

        var btnLayout = btnGO.AddComponent<LayoutElement>();
        btnLayout.minWidth = buttonWidth;
        btnLayout.preferredHeight = buttonHeight;
        btnLayout.flexibleWidth = 0;
    }

    return topBar;
}


        /// Binds an action to a button and updates its label text.
        /// <param name="btn">The button to which the action will be bound.</param>
        /// <param name="label">The text label associated with the button.</param>
        /// <param name="text">The text to set as the label of the button.</param>
        /// <param name="callback">The action that will be executed when the button is clicked.</param>
        public static void BindAcceptButton(Button btn, Text label, string text, UnityAction callback)
        {
            label.text = text;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(callback);
        }
    }
}

/// <summary>
/// Represents a handler that encapsulates a callback action to be invoked when a click event occurs.
/// </summary>
/// <remarks>
/// This class provides a mechanism to handle and execute logic when a click event is triggered.
/// It associates an action defined by a UnityAction delegate with the click event.
/// </remarks>
public class ClickHandler
{
    /// <summary>
    /// A private field that stores the UnityAction delegate to be invoked during a specific click event.
    /// </summary>
    private readonly UnityAction _callback;

    /// Represents a handler that encapsulates a callback action to be invoked when a click event occurs.
    public ClickHandler(UnityAction callback)
    {
        _callback = callback;
    }

    /// Invokes the callback action associated with a click event.
    /// <remarks>
    /// Executes the UnityAction delegate provided during the creation of the ClickHandler instance.
    /// This method is used to process and handle click events associated with the handler.
    /// </remarks>
    public void OnClick()
    {
        _callback.Invoke();
    }
}
