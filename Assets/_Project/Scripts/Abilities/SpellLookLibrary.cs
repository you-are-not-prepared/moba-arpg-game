using UnityEngine;

namespace Moba.Abilities
{
    // The base materials SpellLook tints per hero (Resources/SpellLooks, made by GameBuilder). Runtime copies of these
    // keep the shaders in the builds, which Shader.Find alone wouldn't.
    [CreateAssetMenu(menuName = "MOBA/Spell Look Library", fileName = "SpellLooks")]
    public class SpellLookLibrary : ScriptableObject
    {
        public const string ResourcePath = "SpellLooks";

        [Tooltip("Lit with emission: solid glowing parts (orbs, shards, arrows).")]
        public Material glow;
        [Tooltip("Unlit vertex colour: trails and lightning lines.")]
        public Material lines;
        [Tooltip("Moba/GroundRing: warning circles on the ground.")]
        public Material ring;
        [Tooltip("Moba/MeleeSlash: melee spell swings.")]
        public Material slash;
    }
}
