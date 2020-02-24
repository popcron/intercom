using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Popcron.Intercom
{
    public class BinarySerializer : Serializer
    {
        private static Dictionary<string, Type> nameToType = null;

        /// <summary>
        /// Just a custom type get method.
        /// </summary>
        private static Type GetType(string name)
        {
            if (nameToType == null)
            {
                nameToType = new Dictionary<string, Type>();
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type[] types = assemblies[a].GetTypes();
                    for (int t = 0; t < types.Length; t++)
                    {
                        nameToType[types[t].FullName] = types[t];
                    }
                }
            }

            return nameToType.TryGetValue(name, out Type type) ? type : null;
        }

        public override object Deserialize(byte[] data)
        {
            //first byte is the type length
            byte typeLength = data[0];
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < typeLength; i++)
            {
                char character = (char)data[1 + i];
                builder.Append(character);
            }

            string typeName = builder.ToString();
            Type type = GetType(typeName);
            using (var memStream = new MemoryStream())
            {
                BinaryFormatter binForm = new BinaryFormatter();
                memStream.Write(data, 0, data.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                object obj = binForm.Deserialize(memStream);
                return obj;
            }
        }

        public override byte[] Serialize(object value)
        {
            string typeName = value.GetType().FullName;
            List<byte> data = new List<byte>();
            data.Add((byte)typeName.Length);

            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, value);
                return ms.ToArray();
            }
        }
    }
}