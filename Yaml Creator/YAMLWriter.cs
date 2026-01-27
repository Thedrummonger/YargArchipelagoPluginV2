using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YargArchipelagoPlugin;

namespace Yaml_Creator
{

    public static class YAMLWriter
    {
        public static void WriteToFile(YAMLCore core, string filePath)
        {
            var serializer = new SerializerBuilder()
                .WithTypeConverter(new EnumDescriptionConverter())
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            string yaml = serializer.Serialize(core);
            File.WriteAllText(filePath, yaml);
        }

        public static string SerializeToString(YAMLCore core)
        {
            var serializer = new SerializerBuilder()
                .WithTypeConverter(new EnumDescriptionConverter())
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            return serializer.Serialize(core);
        }
    }

    public class EnumDescriptionConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return type.IsEnum;
        }

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var scalar = parser.Consume<Scalar>();
            string value = scalar.Value;

            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                {
                    var enumValue = (Enum)field.GetValue(null);
                    if (enumValue.ToString() == value)
                    {
                        return enumValue;
                    }
                }
            }
            return Enum.Parse(type, value, true);
        }

        public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
        {
            if (value == null)
            {
                emitter.Emit(new Scalar(string.Empty));
                return;
            }
            string description = ((Enum)value).ToString();

            emitter.Emit(new Scalar(description));
        }
    }
}
