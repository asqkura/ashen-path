using System;
using System.Collections.Generic;

namespace AshenPath.Battle
{
    [Serializable]
    public class BattleDeckPresetData
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public ElementType primaryElement = ElementType.None;
        public List<string> cardIds = new();
    }
}
