using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace ModuleFactory.SimpleUI
{
    public enum UILayer { Normal, Popup, Overlay }

    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class UIPanel : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("Layer")] private UILayer layer = UILayer.Normal;
        [SerializeField, FormerlySerializedAs("CanGoBack")] private bool canGoBack = true;
        [SerializeField, FormerlySerializedAs("Modal")] private bool modal;
        [SerializeField, FormerlySerializedAs("InitiallyOpen")] private bool initiallyOpen;

        public UILayer Layer => layer;
        public bool CanGoBack => canGoBack;
        public bool Modal => modal;
        public bool InitiallyOpen => initiallyOpen;
        public UnityEvent Shown = new UnityEvent();
        public UnityEvent Hidden = new UnityEvent();
        public bool IsOpen { get; internal set; }
        internal UIManager Owner;
        internal int Revision;

        /// <summary>Sets the panel configuration before a UIManager registers it.</summary>
        public void Configure(UILayer layer, bool modal = false, bool canGoBack = true, bool initiallyOpen = false)
        {
            if (Owner != null)
                throw new InvalidOperationException("Configure the panel before registering it with a UIManager.");

            this.layer = layer;
            this.modal = modal;
            this.canGoBack = canGoBack;
            this.initiallyOpen = initiallyOpen;
        }

        private void OnDisable()
        {
            if (Owner != null && IsOpen) Owner.ExternalClose(this);
        }
        private void OnDestroy()
        {
            if (Owner != null) Owner.ExternalClose(this);
        }
    }
}
