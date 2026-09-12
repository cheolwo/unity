using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Ssalddel.Unity.Presentation.World
{
    /// <summary>같은 휴대폰 Canvas·스크롤·탐색·카드 풀을 서버 관찰에 재사용한다.</summary>
    public sealed partial class 동네관찰휴대폰View
    {
        public sealed class 관찰카드
        {
            public string Title = "", Body = "", Chip = "";
            public Color Tint = Color.white, ChipColor = new(.32f, .2f, .5f);
            public Action Click;
            public bool Enabled = true;
        }
        private 관찰카드[] 외부카드;
        private Action 외부홈, 외부뒤로;
        private string 외부제목;
        private readonly List<Text> 외부칩 = new();
        public void 외부관찰표시(string heading, string statusLabel, string launcher, 관찰카드[] cards, Action navigateHome, Action navigateBack, bool resetScroll = false)
        {
            if (panel == null) 구성();
            외부카드 = cards ?? Array.Empty<관찰카드>(); 외부홈 = navigateHome; 외부뒤로 = navigateBack; 외부제목 = heading;
            status.text = statusLabel; iconText.text = "▯  휴대폰\n" + launcher;
            if (IsOpen) 외부그리기();
            if (resetScroll) scroll.verticalNormalizedPosition = 1;
        }
        private void 외부그리기()
        {
            title.text = 외부제목;
            for (int i = 0; i < 외부카드.Length; i++)
            {
                if (rows.Count <= i)
                {
                    var button = 버튼(content, "관찰 카드 " + i, null);
                    button.gameObject.AddComponent<LayoutElement>();
                    var text = button.GetComponentInChildren<Text>(); text.alignment = TextAnchor.MiddleLeft; text.fontSize = 17; text.supportRichText = false;
                    rows.Add(new Card { Root = button.gameObject, Text = text, Button = button });
                }
                while (외부칩.Count <= i)
                {
                    var chipRoot = 상자(rows[외부칩.Count].Root.transform, "상태 칩", Accent);
                    chipRoot.anchorMin = chipRoot.anchorMax = new Vector2(0, 1); chipRoot.pivot = new Vector2(0, 1);
                    chipRoot.anchoredPosition = new Vector2(14, -10); chipRoot.sizeDelta = new Vector2(290, 26);
                    var chip = 글(chipRoot, "상태", 13, Color.white); chip.supportRichText = false; 채움(chip.rectTransform, 8, 0, 8, 0); 외부칩.Add(chip);
                }
                var source = 외부카드[i]; var row = rows[i]; row.Root.SetActive(true); row.Root.name = "관찰 카드 · " + source.Title;
                row.Button.onClick.RemoveAllListeners(); var click = source.Click; if (click != null) row.Button.onClick.AddListener(() => click());
                row.Button.interactable = source.Enabled && click != null;
                row.Button.GetComponent<Image>().color = source.Tint;
                var colors = row.Button.colors; colors.disabledColor = source.Enabled ? Color.white : new Color(.72f, .74f, .77f); row.Button.colors = colors;
                row.Text.color = Ink; row.Text.text = source.Title + (source.Body.Length == 0 ? "" : "\n" + source.Body);
                bool chipVisible = source.Chip.Length > 0;
                외부칩[i].transform.parent.gameObject.SetActive(chipVisible); 외부칩[i].text = source.Chip;
                외부칩[i].transform.parent.GetComponent<Image>().color = source.ChipColor;
                채움(row.Text.rectTransform, 14, chipVisible ? 44 : 10, 14, 10);
                var lines = row.Text.text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / 21f)));
                row.Root.GetComponent<LayoutElement>().preferredHeight = Math.Max(70, 22 + lines * 25 + (chipVisible ? 34 : 0));
            }
            for (int i = 외부카드.Length; i < rows.Count; i++) rows[i].Root.SetActive(false);
        }
    }
}
