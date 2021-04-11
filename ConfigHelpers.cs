using System;
using BepInEx.Configuration;
using UnityEngine.Assertions;

namespace BetterContinents
{
    public static class ConfigFileExtensions
    {
        public static ConfigBuilder Declare(this ConfigFile configFile) => new ConfigBuilder(configFile);
    }

    public class ConfigBuilder
    {
        internal ConfigFile configFile;
        internal int groupIdx = 0;

        internal ConfigBuilder(ConfigFile configFile) => this.configFile = configFile;
        
        public ConfigBuilder AddGroup(string groupNameBase, Action<GroupBuilder> groupBuilder)
        {
            groupBuilder(new GroupBuilder(this, groupNameBase));
            return this;
        }
    }
    
    public class GroupBuilder
    {
        internal readonly ConfigBuilder configBuilder;
        internal readonly string groupName;
        internal int valueIdx = 0;

        internal GroupBuilder(ConfigBuilder configBuilder, string groupNameBase)
        {
            this.configBuilder = configBuilder;
            this.groupName = $"{configBuilder.groupIdx++:00} {groupNameBase}";
        }
        
        public ValueBuilder AddValue(string key) => new ValueBuilder(this, key); //ref ConfigEntry<T> bindTarget, string key, T defaultValue, AcceptableValueBase range, string description);//=> configFile.
    }

    public class ValueBuilder
    {
        private readonly GroupBuilder groupBuilder;
        private readonly string key;
        
        private string description = string.Empty;
        private object defaultValue;
        private AcceptableValueBase range;
        private bool bound;
        private bool isHidden;
        private bool isReadOnly;
        private bool isAdvanced;
        private bool showAsPercent;

        internal ValueBuilder(GroupBuilder groupBuilder, string key)
        {
            Assert.IsFalse(bound, "Already bound this Key");
            this.groupBuilder = groupBuilder;
            this.key = key;
        }

        public ValueBuilder Description(string description)
        {
            Assert.IsFalse(bound, "Already bound this Key");
            this.description = description;
            return this;
        }

        public ValueBuilder Default<T>(T defaultValue)
        {
            Assert.IsFalse(bound, "Already bound this Key");
            this.defaultValue = defaultValue;
            return this;
        }

        public ValueBuilder Range(AcceptableValueBase range)
        {
            Assert.IsFalse(bound, "Already bound this Key");
            this.range = range;
            return this;
        }

        public ValueBuilder Range(float min, float max)
        {
            Assert.IsFalse(bound, "Already bound this Key");
            this.range = new AcceptableValueRange<float>(min, max);
            return this;
        }
        
        public ValueBuilder Range(int min, int max)
        {
            Assert.IsFalse(bound, "Already bound this Key");
            this.range = new AcceptableValueRange<int>(min, max);
            return this;
        }

        public ValueBuilder Hidden()
        {
            this.isHidden = true;
            return this;
        }
        
        public ValueBuilder ReadOnly()
        {
            this.isReadOnly = true;
            return this;
        }
        
        public ValueBuilder Advanced()
        {
            this.isAdvanced = true;
            return this;
        }

        public ValueBuilder ShowAsPercent()
        {
            this.showAsPercent = true;
            return this;
        }
        
        public void Bind<T>(out ConfigEntry<T> bindTarget)
        {
            Assert.IsFalse(bound, "Already bound this Key");
            bindTarget = groupBuilder.configBuilder.configFile.Bind<T>(groupBuilder.groupName, key, defaultValue == null? default(T) : (T)defaultValue,
                new ConfigDescription(description, range, new ConfigurationManagerAttributes
                {
                    Order = --groupBuilder.valueIdx,
                    Browsable = !isHidden,
                    ReadOnly = isReadOnly,
                    IsAdvanced = isAdvanced,
                    ShowRangeAsPercent = showAsPercent,
                }));
            bound = true;
        }
    }
}