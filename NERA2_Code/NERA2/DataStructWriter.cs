using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DataStructUtils
{
    public static class DataStructWriter<T1, T2>
    {
        public delegate T1 keyDelegate(T1 key);
        public delegate T2 valDelegate(T2 value);

        public static void saveDictionary(Dictionary<T1, T2> dictionary, string fileName, bool append, Encoding encoding, char separator, keyDelegate keyDelegate, valDelegate valDelegate)
        {
            StreamWriter wrt = new StreamWriter(fileName, append, encoding);
            T1[] keys = dictionary.Keys.ToArray();

            for (int i = 0; i < keys.Length; i++)
            {
                T1 key = keys[i];
                T2 value = dictionary[key];

                if (keyDelegate != null)
                {
                    key = keyDelegate(key);
                }
                if (valDelegate != null)
                {
                    value = valDelegate(value);
                }

                wrt.WriteLine("{0}{1}{2}", key.ToString(), separator.ToString(), value.ToString());
            }
            wrt.AutoFlush = true;
            wrt.Close();
        }

        public static void saveDictionary(Dictionary<T1, List<T2>> dictionary, string fileName, bool append, Encoding encoding, char separator_1, char separator_2, keyDelegate keyDelegate, valDelegate valDelegate)
        {
            StreamWriter wrt = new StreamWriter(fileName, append, encoding);
            T1[] keys = dictionary.Keys.ToArray();

            for (int i = 0; i < keys.Length; i++)
            {
                T1 key = keys[i];
                if (keyDelegate != null)
                {
                    key = keyDelegate(key);
                }

                wrt.Write("{0}{1}", key.ToString(), separator_1.ToString());

                T2 value;
                if (dictionary[key].Count > 0)
                {
                    if (dictionary[key].Count > 1)
                    {
                        for (int j = 0; j < dictionary[key].Count - 1; j++)
                        {
                            value = dictionary[key][j];
                            if (valDelegate != null)
                            {
                                value = valDelegate(value);
                            }
                            wrt.Write("{0}{1}", value.ToString(), separator_2.ToString());
                        }
                    }

                    value = dictionary[key][dictionary[key].Count - 1];
                    if (valDelegate != null)
                    {
                        value = valDelegate(value);
                    }
                    wrt.WriteLine(value.ToString());
                }
            }
            wrt.AutoFlush = true;
            wrt.Close();
        }
    }

    public static class DataStructWriter<T>
    {
        public delegate T keyDelegate(T key);

        public static void saveHashSet(HashSet<T> hashSet, string fileName, bool append, Encoding encoding, keyDelegate keyDelegate)
        {
            StreamWriter wrt = new StreamWriter(fileName, append, encoding);
            T[] objects = hashSet.ToArray();

            for (int i = 0; i < objects.Length; i++)
            {
                T obj = objects[i];

                if (keyDelegate != null)
                {
                    obj = keyDelegate(obj);
                }

                wrt.WriteLine(obj.ToString());
            }
            wrt.AutoFlush = true;
            wrt.Close();
        }
    }
}
