using UnityEngine.UIElements;

namespace Moba.UI
{
    // "● Fire" in the type's colour: a hero's base damage type, on the menus and the scoreboard. Styled by the
    // damage-type classes in Frontend.uss and Hud.uss.
    public static class DamageTypeBadge
    {
        public static VisualElement Create(DamageType type, string className = null)
        {
            var badge = new VisualElement { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("damage-type");
            if (className != null) badge.AddToClassList(className);
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("damage-type__dot");
            dot.style.backgroundColor = DamageTypes.ColorOf(type);
            badge.Add(dot);
            var name = new Label(DamageTypes.Name(type)) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("damage-type__name");
            name.style.color = DamageTypes.ColorOf(type);
            badge.Add(name);
            return badge;
        }
    }
}
