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
        public string MethodName { get; private set; } = null;

        /// <summary>
        /// The parameters of this invocation.
        /// </summary>
        public object[] Parameters { get; private set; } = { };

        /// <summary>
        /// Returns the data in byte array format that should be sent to the buffer.
        /// </summary>
        public byte[] GetData(Serializer serializer)
        {
            //first serialize the name here
            List<byte> data = new List<byte>();
            if (string.IsNullOrEmpty(MethodName))
            {
                data.Add(0);
            }
            else
            {
                byte[] methodNameBytes = Encoding.UTF8.GetBytes(MethodName);
                data.Add((byte)methodNameBytes.Length);
                if (methodNameBytes.Length > 0)
                {
                    data.AddRange(methodNameBytes);
                }
            }

            //then serialize the params if any
            data.Add((byte)Parameters.Length);
            if (Parameters.Length > 0)
            {
                for (int i = 0; i < Parameters.Length; i++)
                {
                    byte[] paramData = serializer.Serialize(Parameters[i]);
                    data.AddRange(BitConverter.GetBytes(paramData.Length));
                    data.AddRange(paramData);
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
            Parameters = parameters;
        }

        public Invocation(byte[] data, Serializer serializer)
        {
            int position = 0;

            //read the method name out first
            byte length = data[position];
            position++;

            if (length == 0)
            {
                MethodName = null;
            }
            else
            {
                MethodName = Encoding.UTF8.GetString(data, position, length);
                position += length;
            }

            //then read the param length
            byte paramsLength = data[position];
            position++;

            List<object> parameters = new List<object>();
            for (int i = 0; i < paramsLength; i++)
            {
                //read the data size length
                int paramDataSize = BitConverter.ToInt32(data, position);
                position += 4;

                //copy the data then
                byte[] paramData = new byte[paramDataSize];
                Array.Copy(data, position, paramData, 0, paramDataSize);
                position += paramDataSize;

                object parameter = serializer.Deserialize(paramData);
                parameters.Add(parameter);
            }

            Parameters = parameters.ToArray();
        }
    }
}