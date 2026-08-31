using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ending 回顾页可选配图（无 Sprite 时用 <see cref="RemiSharedExperienceCatalog"/> 占位色）。</summary>
[CreateAssetMenu(fileName = "RemiSharedExperienceEndingCatalog", menuName = "Remi/Memory/Shared Experience Ending Catalog")]
public class RemiSharedExperienceEndingCatalog : ScriptableObject
{
    [Serializable]
    public class IllustrationEntry
    {
        public RemiSharedExperienceId experienceId;
        public Sprite illustration;
    }

    public List<IllustrationEntry> illustrations = new List<IllustrationEntry>();

    public Sprite GetIllustration(RemiSharedExperienceId experienceId)
    {
        if (illustrations == null)
            return null;

        foreach (IllustrationEntry entry in illustrations)
        {
            if (entry != null && entry.experienceId == experienceId && entry.illustration != null)
                return entry.illustration;
        }

        return null;
    }

    public Sprite GetIllustration(string idKey)
    {
        return RemiSharedExperienceCatalog.TryParseIdKey(idKey, out RemiSharedExperienceId id)
            ? GetIllustration(id)
            : null;
    }
}
