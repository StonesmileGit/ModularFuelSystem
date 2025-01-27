using System.Linq;
using UnityEngine;

using System.Collections.Generic;

namespace RealFuels
{
    public class ModulePatchableEngineConfigs : ModuleEngineConfigs
    {
        public const string PatchNodeName = "SUBCONFIG";
        protected const string PatchNameKey = "__mpecPatchName";

        [KSPField(isPersistant = true)]
        public string activePatchName = "";

        [KSPField(isPersistant = true)]
        public bool dynamicPatchApplied = false;

        protected bool ConfigHasPatch(ConfigNode config) => GetPatchesOfConfig(config).Count > 0;

        protected List<ConfigNode> GetPatchesOfConfig(ConfigNode config)
        {
            ConfigNode[] list = config.GetNodes(PatchNodeName);
            List<ConfigNode> sortedList = ConfigFilters.Instance.FilterDisplayConfigs(list.ToList());
            return sortedList;
        }

        protected ConfigNode GetPatch(string configName, string patchName)
        {
            return GetPatchesOfConfig(GetConfigByName(configName))
                .FirstOrDefault(patch => patch.GetValue("name") == patchName);
        }

        protected bool ConfigIsPatched(ConfigNode config) => config.HasValue(PatchNameKey);

        // TODO: This is called a lot, performance concern?
        protected ConfigNode PatchConfig(ConfigNode parentConfig, ConfigNode patch, bool dynamic)
        {
            var patchedNode = parentConfig.CreateCopy();

            foreach (var key in patch.values.DistinctNames())
                patchedNode.RemoveValues(key);
            foreach (var nodeName in patch.nodes.DistinctNames())
                patchedNode.RemoveNodes(nodeName);

            patch.CopyTo(patchedNode);

            patchedNode.SetValue("name", parentConfig.GetValue("name"));
            if (!dynamic)
                patchedNode.AddValue(PatchNameKey, patch.GetValue("name"));
            return patchedNode;
        }

        public ConfigNode GetNonDynamicPatchedConfiguration() => GetSetConfigurationTarget(configuration);

        public void ApplyDynamicPatch(ConfigNode patch)
        {
            // Debug.Log($"**RFMPEC** dynamic patch applied to active config `{configurationDisplay}`");
            SetConfiguration(PatchConfig(GetNonDynamicPatchedConfiguration(), patch, true), false);
            dynamicPatchApplied = true;
        }

        protected override ConfigNode GetSetConfigurationTarget(string newConfiguration)
        {
            if (activePatchName == "")
                return base.GetSetConfigurationTarget(newConfiguration);
            return PatchConfig(GetConfigByName(newConfiguration), GetPatch(newConfiguration, activePatchName), false);
        }

        public override void SetConfiguration(string newConfiguration = null, bool resetTechLevels = false)
        {
            base.SetConfiguration(newConfiguration, resetTechLevels);
            if (dynamicPatchApplied)
            {
                dynamicPatchApplied = false;
                part.SendMessage("OnMPECDynamicPatchReset", SendMessageOptions.DontRequireReceiver);
            }
        }

        public override int UpdateSymmetryCounterparts()
        {
            DoForEachSymmetryCounterpart((engine) =>
                (engine as ModulePatchableEngineConfigs).activePatchName = activePatchName);
            return base.UpdateSymmetryCounterparts();
        }

        public override string GetConfigInfo(ConfigNode config, bool addDescription = true, bool colorName = false)
        {
            var info = base.GetConfigInfo(config, addDescription, colorName);

            if (!ConfigHasPatch(config) || ConfigIsPatched(config))
                return info;

            if (addDescription) info += "\n";
            foreach (var patch in GetPatchesOfConfig(config))
                info += ConfigInfoString(PatchConfig(config, patch, false), false, colorName);
            return info;
        }

        public override string GetConfigDisplayName(ConfigNode node)
        {
            if (node.HasValue("displayName"))
                return node.GetValue("displayName");
            var name = node.GetValue("name");
            if (!node.HasValue(PatchNameKey))
                return name;
            return $"{name} [Subconfig {node.GetValue(PatchNameKey)}]";
        }

        protected override void DrawConfigSelectors(IEnumerable<ConfigNode> availableConfigNodes)
        {
            foreach (var node in availableConfigNodes)
            {
                DrawSelectButton(
                    node,
                    node.GetValue("name") == configuration && activePatchName == "",
                    (configName) =>
                    {
                        activePatchName = "";
                        GUIApplyConfig(configName);
                    });
                foreach (var patch in GetPatchesOfConfig(node))
                {
                    var patchedNode = PatchConfig(node, patch, false);
                    string patchName = patch.GetValue("name");
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(30);
                        DrawSelectButton(
                            patchedNode,
                            node.GetValue("name") == configuration && patchName == activePatchName,
                            (configName) =>
                            {
                                activePatchName = patchName;
                                GUIApplyConfig(configName);
                            });
                    }
                }
            }
        }
    }
}
