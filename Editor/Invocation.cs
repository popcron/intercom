using System;
using System.Collections.Generic;
using System.Text;

namespace Popcron.Intercom
{
    [Serializable]
    public class Invocation
    {
        /// <summary>
        /// The static method that is meant to be invoked.
        /// </summary>
        public string MethodName { get; private set; }

        /// <summary>
        /// Returns the size of this invocation in bytes.
        /// </summary>
        public int GetSize()
        {
            int methodNameLength = 1;
            if (MethodName != null)
            {
                methodNameLength += 4 + (MethodName.Length * 2);
            }

            return methodNameLength;
        }

        /// <summary>
        /// Returns the data in byte array format that should be sent to the buffer.
        /// </summary>
        public byte[] GetData()
        {
            //first byte indicates if the string is null or not
            //next 4 bytes indicate the length of the string
            //then every 2 bytes indicate the char

            List<byte> data = new List<byte>();
            if (MethodName == null)
            {
                data.Add(0);
            }
            else
            {
                data.Add(1);
                data.AddRange(BitConverter.GetBytes(MethodName.Length));
                for (int i = 0; i < MethodName.Length; i++)
                {
                    data.AddRange(BitConverter.GetBytes(MethodName[i]));
                }
            }

            return data.ToArray();
        }

        private Invocation()
        {

        }

        public Invocation(string methodName, params object[] parameters)
        {
            MethodName = methodName;
        }

        public Invocation(byte[] data)
        {
            if (data.Length == 0 || data[0] == 0)
            {
                MethodName = null;
            }
            else
            {
                int length = BitConverter.ToInt32(data, 1);
                StringBuilder builder = new StringBuilder(length);
                for (int i = 0; i < length; i++)
                {
                    char character = BitConverter.ToChar(data, 2 + i);
                    builder.Append(character);
                }

                MethodName = builder.ToString();
            }
        }
    }
}