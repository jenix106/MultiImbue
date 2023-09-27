using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace MultiImbue
{
    public class ImbueManager : ThunderScript
    {
        [ModOption(name: "Apply to Items on spawn", tooltip: "Applies multi-imbuing to Items when they're spawned in", valueSourceName: nameof(booleanOption), defaultValueIndex = 1)]
        public static bool AllItems;
        public static List<Item> items = new List<Item>();
        public static ModOptionBool[] booleanOption =
        {
            new ModOptionBool("Enabled", true),
            new ModOptionBool("Disabled", false)
        };
        public override void ScriptEnable()
        {
            base.ScriptEnable();
            EventManager.onItemSpawn += EventManager_onItemSpawn;
        }

        private void EventManager_onItemSpawn(Item item)
        {
            if(AllItems && item.data.GetModule<ImbueModule>() == null)
            {
                items.Add(item);
                item.gameObject.AddComponent<ImbueComponent>();
            }
        }
    }
    public class ImbueModule : ItemModule
    {
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<ImbueComponent>();
        }
    }
    public class ImbueComponent : MonoBehaviour
    {
        Item item;
        Dictionary<ColliderGroup, Dictionary<string, Imbue>> imbues = new Dictionary<ColliderGroup, Dictionary<string, Imbue>>();
        Dictionary<Imbue, Color> colors = new Dictionary<Imbue, Color>();
        float fadeStart = 0;
        float fadeTime = 1;
        int index = 0;
        public void Start()
        {
            item = GetComponent<Item>();
            foreach (ColliderGroup group in item.colliderGroups)
            {
                if (group.imbue != null)
                {
                    group.imbue.onImbueSpellChange += Imbue_onImbueSpellChange;
                    imbues.Add(group, new Dictionary<string, Imbue>());
                }
            }
        }
        public void Update()
        {
            foreach (ColliderGroup group in imbues.Keys)
            {
                if (fadeStart < fadeTime && group.imbueEmissionRenderer != null && colors.Count > 0)
                {
                    fadeStart += Time.deltaTime;
                    Color color1 = colors.ElementAtOrDefault(index).Value;
                    Color color2 = colors.ElementAtOrDefault(index + 1 > colors.Count - 1 ? 0 : index + 1).Value;
                    Imbue imbue1 = colors.ElementAtOrDefault(index).Key;
                    Imbue imbue2 = colors.ElementAtOrDefault(index + 1 > colors.Count - 1 ? 0 : index + 1).Key;
                    float intensity1 = Mathf.InverseLerp(0.0f, Mathf.Max(0.0f, imbue1.maxEnergy), Mathf.Max(0.0f, imbue1.energy));
                    float intensity2 = Mathf.InverseLerp(0.0f, Mathf.Max(0.0f, imbue2.maxEnergy), Mathf.Max(0.0f, imbue2.energy));
                    group.imbueEmissionRenderer.material.SetColor(Shader.PropertyToID("_EmissionColor"), Color.Lerp(color1 * intensity1, color2 * intensity2, fadeStart));
                }
                else
                {
                    ++index;
                    if (index > colors.Count - 1) index = 0;
                    fadeStart = 0;
                }
            }
        }

        private void Imbue_onImbueSpellChange(SpellCastCharge spellData, float amount, float change, EventTime eventTime)
        {
            if (eventTime == EventTime.OnEnd && spellData != null)
            {
                if (imbues[spellData?.imbue?.colliderGroup].ContainsKey(spellData?.id))
                {
                    Imbue imbue = imbues[spellData.imbue.colliderGroup][spellData.id];
                    imbue.Transfer(spellData, amount);
                    if (spellData.imbueEffect?.effects?.Where(match => match is EffectShader)?.FirstOrDefault() is EffectShader effect && spellData.imbue.colliderGroup.imbueEmissionRenderer != null)
                    {
                        if (!colors.ContainsKey(imbue))
                        {
                            Color color = ((EffectModuleShader)effect.module).mainColorEnd;
                            colors.Add(imbue, color);
                        }
                        effect.Despawn();
                    }
                    spellData.imbue?.colliderGroup?.imbue?.Stop();
                }
                else if (!imbues[spellData?.imbue?.colliderGroup].ContainsKey(spellData.id))
                {
                    Imbue imbue = spellData?.imbue?.colliderGroup?.gameObject.AddComponent<Imbue>();
                    imbue.colliderGroup = spellData?.imbue?.colliderGroup;
                    imbue.maxEnergy = spellData.imbue.maxEnergy;
                    imbues[imbue.colliderGroup].Add(spellData.id, imbue);
                    if (spellData.imbueEffect?.effects?.Where(match => match is EffectShader)?.FirstOrDefault() is EffectShader effect && spellData.imbue.colliderGroup.imbueEmissionRenderer != null)
                    {
                        if (!colors.ContainsKey(imbue))
                        {
                            Color color = ((EffectModuleShader)effect.module).mainColorEnd;
                            colors.Add(imbue, color);
                        }
                        effect.Despawn();
                    }
                    item.imbues.Add(imbue);
                    imbue.Transfer(spellData, amount);
                    spellData.imbue?.colliderGroup?.imbue?.Stop();
                    imbue.onImbueEnergyDrained += Imbue_onImbueEnergyDrained;
                }
            }
        }

        private void Imbue_onImbueEnergyDrained(SpellCastCharge spellData, float amount, float change, EventTime eventTime)
        {
            if(eventTime == EventTime.OnStart && spellData != null)
            {
                colors.Remove(spellData.imbue);
                index = 0;
            }
        }
    }
}
