using System.Reflection;
using Rainbow.Filtering;
using Rainbow.Storage.Yaml;
using Sitecore.Diagnostics;

namespace Rainbow.Storage.AzureBlob.Utils
{
    public static class CloneHelper
    {
        public static YamlSerializationFormatter Clone(this YamlSerializationFormatter obj)
        {
            return YamlSerializationFormatterClone.Clone(obj);
        }
        
        public class YamlSerializationFormatterClone : YamlSerializationFormatter
        {
            public static YamlSerializationFormatterClone Clone(YamlSerializationFormatter obj)
            {
                Assert.ArgumentNotNull(obj, nameof(obj));
                var fieldFilter = typeof(YamlSerializationFormatter)
                    .GetField("_fieldFilter", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(obj) as IFieldFilter;

                return new YamlSerializationFormatterClone(fieldFilter, obj)
                {
                    ParentDataStore = obj.ParentDataStore
                };
            }

            private YamlSerializationFormatterClone(
                IFieldFilter fieldFilter, 
                YamlSerializationFormatter formatter) 
                : base(fieldFilter, formatter.FieldFormatters.ToArray())
            {
            }
        }
    }
}