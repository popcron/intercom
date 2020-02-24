using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Popcron.Intercom
{
    [Serializable]
    public class Intercom
    {
        /// <summary>
        /// 8MB capacity here yo.
        /// </summary>
        public const int Capacity = 1024 * 1024 * 8;

        public delegate void OnInvoked(Invocation inv);

        private static List<MethodInfo> methods = null;

        private Random random = new Random(DateTime.Now.Millisecond);
        private MemoryMappedViewAccessor fooView = null;
        private MemoryMappedViewAccessor barView = null;
        private MemoryMappedViewAccessor sharedView = null;
        private List<Invocation> queue = new List<Invocation>();
        private List<long> pastMessages = new List<long>();

        /// <summary>
        /// The side that this intercom is taking part of.
        /// </summary>
        public IntercomSide MySide { get; private set; }

        /// <summary>
        /// The unique identifier that should match the other intercom object.
        /// </summary>
        public string Identifier { get; private set; } = null;

        /// <summary>
        /// The serializer to use when parsing data.
        /// </summary>
        public Serializer Serializer { get; set; } = new BinarySerializer();

        /// <summary>
        /// The intercom side that is opposite of this one.
        /// </summary>
        public IntercomSide OtherSide
        {
            get
            {
                if (MySide == IntercomSide.Foo)
                {
                    return IntercomSide.Bar;
                }
                else if (MySide == IntercomSide.Bar)
                {
                    return IntercomSide.Foo;
                }

                throw new Exception();
            }
        }

        /// <summary>
        /// Generates a unique long number.
        /// </summary>
        private long GetUniqueID()
        {
            long min = long.MinValue;
            long max = long.MaxValue;

            //Working with ulong so that modulo works correctly with values > long.MaxValue
            ulong uRange = (ulong)(max - min);

            //Prevent a modolo bias; see https://stackoverflow.com/a/10984975/238419
            //for more information.
            //In the worst case, the expected number of calls is 2 (though usually it's
            //much closer to 1) so this loop doesn't really hurt performance at all.
            ulong ulongRand;
            do
            {
                byte[] buf = new byte[8];
                random.NextBytes(buf);
                ulongRand = (ulong)BitConverter.ToInt64(buf, 0);
            }
            while (ulongRand > ulong.MaxValue - ((ulong.MaxValue % uRange) + 1) % uRange);

            return (long)(ulongRand % uRange) + min;
        }

        /// <summary>
        /// The shared buffer that both sides take advantage of.
        /// 0 = Foo has finishe dreading.
        /// 1 = Bar has finished reading.
        /// </summary>
        private MemoryMappedViewAccessor Shared
        {
            get
            {
                if (sharedView == null)
                {
                    try
                    {
                        string key = $"{Identifier}.Shared";
                        MemoryMappedFile memoryFile = MemoryMappedFile.CreateOrOpen(key, 2 + 4, MemoryMappedFileAccess.ReadWrite);
                        sharedView = memoryFile.CreateViewAccessor();
                    }
                    catch
                    {

                    }
                }

                return sharedView;
            }
        }

        private MemoryMappedViewAccessor Input
        {
            get
            {
                if (MySide == IntercomSide.Bar)
                {
                    if (barView == null)
                    {
                        try
                        {
                            string key = $"{Identifier}.{OtherSide}";
                            MemoryMappedFile memoryFile = MemoryMappedFile.CreateOrOpen(key, Capacity, MemoryMappedFileAccess.ReadWrite);
                            barView = memoryFile.CreateViewAccessor();
                        }
                        catch
                        {

                        }
                    }

                    return barView;
                }
                else if (MySide == IntercomSide.Foo)
                {
                    if (fooView == null)
                    {
                        try
                        {
                            string key = $"{Identifier}.{OtherSide}";
                            MemoryMappedFile memoryFile = MemoryMappedFile.CreateOrOpen(key, Capacity, MemoryMappedFileAccess.ReadWrite);
                            fooView = memoryFile.CreateViewAccessor();
                        }
                        catch
                        {

                        }
                    }

                    return fooView;
                }
                else
                {
                    return null;
                }
            }
        }

        private MemoryMappedViewAccessor Output
        {
            get
            {
                if (MySide == IntercomSide.Bar)
                {
                    if (fooView == null)
                    {
                        string key = $"{Identifier}.{MySide}";
                        MemoryMappedFile memoryFile = MemoryMappedFile.CreateOrOpen(key, Capacity, MemoryMappedFileAccess.ReadWrite);
                        fooView = memoryFile.CreateViewAccessor();
                    }

                    return fooView;
                }
                else if (MySide == IntercomSide.Foo)
                {
                    if (barView == null)
                    {
                        string key = $"{Identifier}.{MySide}";
                        MemoryMappedFile memoryFile = MemoryMappedFile.CreateOrOpen(key, Capacity, MemoryMappedFileAccess.ReadWrite);
                        barView = memoryFile.CreateViewAccessor();
                    }

                    return barView;
                }
                else
                {
                    return null;
                }
            }
        }

        static Intercom()
        {
            EnsureMethodsExist();
        }

        ~Intercom()
        {
            fooView?.Dispose();
            barView?.Dispose();
            sharedView?.Dispose();
        }

        public Intercom(IntercomSide source, string identifier)
        {
            MySide = source;
            Identifier = identifier;
            Invoke("Hello World!");
        }

        /// <summary>
        /// Sends the other intercom an invoke request.
        /// </summary>
        public void Invoke(string methodName, params object[] parameters)
        {
            //store this message in a pool
            Invocation message = new Invocation(methodName, parameters);
            queue.Add(message);
        }

        public async Task InvokeTask(string methodName, params object[] parameters)
        {
            int frame = GetCurrentFrame(OtherSide);

            //store this message in a pool
            Invocation message = new Invocation(methodName, parameters);
            queue.Add(message);

            //wait for it to get delievered by checking when the other side went up
            while (frame >= GetCurrentFrame(OtherSide))
            {
                await Task.Delay(1);
            }
        }

        /// <summary>
        /// Is this intercom finished reading?
        /// </summary>
        public ReadingState GetReadingState(IntercomSide source)
        {
            return (ReadingState)Shared.ReadByte((long)source);
        }

        /// <summary>
        /// Sets the read state of this source to a value.
        /// </summary>
        public void SetReadingState(IntercomSide source, ReadingState state)
        {
            Shared.Write((long)source, (byte)state);
        }

        /// <summary>
        /// The current execution frame for this side.
        /// </summary>
        public int GetCurrentFrame(IntercomSide side)
        {
            return Shared.ReadInt32((long)side + 2);
        }

        /// <summary>
        /// Sets the execution from for this side.
        /// </summary>
        public void SetCurrentFrame(IntercomSide side, int value)
        {
            Shared.Write((long)side + 2, value);
        }

        /// <summary>
        /// Sends the entire queue.
        /// </summary>
        private void SendQueue()
        {
            long position = 0;

            //add unique id
            Output.Write(position, GetUniqueID());
            position += sizeof(long);

            //add how many messages will be sent as well
            Output.Write(position, queue.Count);
            position += sizeof(int);

            //then add all of the data in here
            for (int p = 0; p < queue.Count; p++)
            {
                Invocation message = queue[p];
                SendSingleMessage(message, ref position);
            }

            queue.Clear();
        }

        /// <summary>
        /// Writes this message at this position in the buffer.
        /// </summary>
        private void SendSingleMessage(Invocation invoke, ref long position)
        {
            if (invoke == null)
            {
                return;
            }

            //add the message length
            byte[] data = invoke.GetData(Serializer);
            int length = data.Length;
            Output.Write(position, length);
            position += sizeof(int);

            //add all of the content
            for (int i = 0; i < length; i++)
            {
                Output.Write(position, data[i]);
                position++;
            }
        }

        /// <summary>
        /// This should be done very frequently.
        /// </summary>
        public void Poll(OnInvoked callback = null)
        {
            SetCurrentFrame(MySide, GetCurrentFrame(MySide) + 1);
            MemoryMappedViewAccessor input = Input;
            if (input == null)
            {
                return;
            }

            //the other application finished polling all messages
            if (GetReadingState(OtherSide) == ReadingState.Finished)
            {
                SetReadingState(OtherSide, ReadingState.Reading);

                //send out the next batch of messages
                if (queue.Count > 0)
                {
                    SendQueue();
                }
            }

            int position = 0;

            //read the next 8 bytes as an id
            long id = input.ReadInt64(position);
            position += sizeof(long);

            //this message was already processed before
            if (pastMessages.Contains(id))
            {
                //so were done then
                SetReadingState(MySide, ReadingState.Finished);
                return;
            }

            //next 4 bytes is how many messages there are
            int messages = input.ReadInt32(position);
            position += sizeof(int);

            if (messages > 0)
            {
                List<Invocation> invokes = new List<Invocation>();
                for (int m = 0; m < messages; m++)
                {
                    //read message length
                    int length = input.ReadInt32(position);
                    position += sizeof(int);

                    //read message data itself
                    byte[] data = new byte[length];
                    for (int i = 0; i < length; i++)
                    {
                        data[i] = input.ReadByte(position);
                        position++;
                    }

                    //process data
                    if (data.Length > 0)
                    {
                        Invocation newInoke = new Invocation(data, Serializer);
                        invokes.Add(newInoke);
                    }
                }

                for (int i = 0; i < invokes.Count; i++)
                {
                    //try and invoke globally
                    InvokeCallbacks(invokes[i], callback);
                }

                SetReadingState(MySide, ReadingState.Finished);
                pastMessages.Add(id);
            }
            else
            {
                SetReadingState(MySide, ReadingState.Finished);
            }
        }

        private static void EnsureMethodsExist()
        {
            //oops, cache is missing, o-oh
            if (methods == null)
            {
                methods = new List<MethodInfo>();
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type[] types = assemblies[a].GetTypes();
                    for (int t = 0; t < types.Length; t++)
                    {
                        MethodInfo[] methods = types[t].GetMethods();
                        for (int m = 0; m < methods.Length; m++)
                        {
                            if (methods[m].IsStatic)
                            {
                                bool hasAttribute = methods[m].GetCustomAttributes(typeof(InvokeyAttribute), false).Any();
                                if (hasAttribute)
                                {
                                    Intercom.methods.Add(methods[m]);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void InvokeCallbacks(Invocation inv, OnInvoked callback)
        {
            EnsureMethodsExist();

            //try and do globally first
            for (int i = 0; i < methods.Count; i++)
            {
                if (methods[i].Name == inv.MethodName)
                {
                    ParameterInfo[] parameterInfo = methods[i].GetParameters();
                    if (parameterInfo.Length == inv.Parameters.Length)
                    {
                        //try and match these params
                        bool match = true;
                        for (int p = 0; p < parameterInfo.Length; p++)
                        {
                            Type source = parameterInfo[p].ParameterType;
                            Type incoming = inv.Parameters[p].GetType();

                            //strictly the same type
                            if (source == incoming)
                            {
                                continue;
                            }

                            //eh close enough through inheritance
                            if (source.IsSubclassOf(incoming))
                            {
                                continue;
                            }

                            match = false;
                            break;
                        }

                        //cool match bro, hit it
                        if (match)
                        {
                            CallMethod(methods[i], inv);
                        }
                    }
                }
            }

            callback?.Invoke(inv);
        }

        private async void CallMethod(MethodInfo method, Invocation inv)
        {
            object result = method.Invoke(null, inv.Parameters);

            //thing is a task, so await it
            if (result != null && result is Task task)
            {
                await task;
            }

            inv.FinishedExecuting();
        }
    }
}