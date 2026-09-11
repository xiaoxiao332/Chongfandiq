using System;
using UnityEngine;

namespace LastLight
{
    public enum UILayer { BG, Window, Pop, Over }
    [Serializable]
    public sealed class PanelDefinition
    {
        public string Id;
        public string Address;
        public UILayer Layer;
        public bool Modal;
        public bool Pause;
        public bool Closable = true;
        public bool Cache = true;
        public SystemLifetime Lifetime = SystemLifetime.Scene;
    }
    [CreateAssetMenu(menuName = "LastLight/Panel Catalog")]
    public sealed class PanelCatalog : ScriptableObject
    {
        public PanelDefinition[] Panels = Array.Empty<PanelDefinition>();
        public PanelDefinition Find(string id)
        {
            foreach (var definition in Panels) if (definition.Id == id) return definition;
            throw new ArgumentException("未配置面板: " + id);
        }
    }
}
