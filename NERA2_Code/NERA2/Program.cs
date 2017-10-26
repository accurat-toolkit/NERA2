using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using DataStructUtils;
using System.Text.RegularExpressions;
using System.Globalization;

namespace NERA2
{
    class Program
    {
        struct Entity
        {
            public string occurence;
            public string type;
        }

        static void Main(string[] args)
        {
            string srcLang = "en";
            string trgLang = "ro";
            string inputFileName = "";
            string outputFileName = "";
            bool additionalAnnotation = false;

            double score = computeCognateScore("romania", "germania");

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--source")
                {
                    if (i + 1 < args.Length)
                    {
                        srcLang = args[i + 1];
                    }
                }
                else if (args[i] == "--target")
                {
                    if (i + 1 < args.Length)
                    {
                        trgLang = args[i + 1];
                    }
                }
                else if (args[i] == "--input")
                {
                    if (i + 1 < args.Length)
                    {
                        inputFileName = args[i + 1];
                    }
                }
                else if (args[i] == "--output")
                {
                    if (i + 1 < args.Length)
                    {
                        outputFileName = args[i + 1];
                    }
                }
                else if (args[i] == "--param")
                {
                    if (i + 1 < args.Length && args[i + 1].ToLower() == "aa=true")
                    {
                        additionalAnnotation = true;
                    }
                }
            }

            string teDic = srcLang + "_" + trgLang;
            if (inputFileName == "" || outputFileName == "" || !File.Exists(teDic))
            {
                Console.WriteLine("Usage: NERA2.exe --input [FILE] --output [FILE]] [--source [LANG]] [--target [LANG]] [--param [aa]=[TRUE]/[FALSE]]");
                Console.WriteLine("Default Source Language: en");
                Console.WriteLine("Default Target Language: ro");
                Console.WriteLine("TE model file name should be: srcLang_trgLang");
                Console.WriteLine("Parameter aa stands for signaling the existance of additional xml-like markup in the input files.");
                Console.WriteLine("");
                Console.WriteLine("Press ENTER to continue...");
                Console.ReadLine();
            }
            else
            {
                map(teDic, inputFileName, outputFileName, srcLang, trgLang, additionalAnnotation);
            }
        }

        private static void map(string teModelFileName, string inputFileName, string outputFileName, string srcLang, string trgLang, bool additionalAnnotation)
        {
            Dictionary<string, Dictionary<string, double>> teModel = readTE(teModelFileName);
            Dictionary<string, double> matches = new Dictionary<string, double>();

            StreamReader rdr = new StreamReader(inputFileName, Encoding.UTF8);
            string line = "";
            while ((line = rdr.ReadLine()) != null)
            {
                string[] parts = line.Trim().Split('\t');

                Console.Write("Processing pair {0} - {1} ... ", parts[0], parts[1]);
                List<Entity> srcNES = new List<Entity>();
                List<Entity> trgNES = new List<Entity>();
                readNamedEntities(parts[0], parts[1], ref srcNES, ref trgNES, additionalAnnotation);
                match(srcNES, trgNES, teModel, ref matches);
                Console.WriteLine("done!");
            }
            rdr.Close();

            DataStructWriter<string, double>.saveDictionary(matches, outputFileName, false, Encoding.UTF8, '\t', null, null);
            Console.WriteLine("");
            Console.WriteLine("Job finished. Press ENTER to continue...");
            Console.ReadLine();
        }

        private static void match(List<Entity> srcNES, List<Entity> trgNES, Dictionary<string, Dictionary<string, double>> teModel, ref Dictionary<string, double> ret)
        {
            Dictionary<string, Dictionary<string, int>> nesSrc = count(srcNES);
            Dictionary<string, Dictionary<string, int>> nesTrg = count(trgNES);

            double threshold = 0.6;

            //identical form, identical type
            foreach (string key in nesSrc.Keys.ToArray())
            {
                if (nesTrg.ContainsKey(key))
                {
                    List<string> intersection = new List<string>(nesSrc[key].Keys.ToArray().Intersect(nesTrg[key].Keys.ToArray()));
                    if (intersection.Count > 0)
                    {
                        string k = key + "\t" + key;
                        if (!ret.ContainsKey(k))
                        {
                            ret.Add(k, 1.0);
                        }
                        nesSrc.Remove(key);
                        nesTrg.Remove(key);
                    }
                }
            }

            //identical form
            foreach (string key in nesSrc.Keys.ToArray())
            {
                if (nesTrg.ContainsKey(key))
                {
                    string k = key + "\t" + key;
                    if (!ret.ContainsKey(k))
                    {
                        ret.Add(k, 1.0);
                    }
                    nesSrc.Remove(key);
                    nesTrg.Remove(key);
                }
            }

            //different forms, identical type
            foreach (string key in nesSrc.Keys.ToArray())
            {
                HashSet<string> toRemove1 = new HashSet<string>();
                HashSet<string> toRemove2 = new HashSet<string>();

                foreach (string key2 in nesTrg.Keys.ToArray())
                {
                    List<string> intersection = new List<string>(nesSrc[key].Keys.ToArray().Intersect(nesTrg[key2].Keys.ToArray()));
                    if (intersection.Count > 0)
                    {
                        double cognateScore = computeCognateScore(key, key2);
                        double teScore = computeTeScore(key, key2, teModel);

                        if (cognateScore >= threshold)
                        {
                            string k = key + "\t" + key2;
                            if (!ret.ContainsKey(k))
                            {
                                ret.Add(k, Math.Max(cognateScore, teScore));
                            }
                            toRemove1.Add(key);
                            toRemove2.Add(key2);
                        }
                        else if (teScore >= threshold)
                        {
                            string k = key + "\t" + key2;
                            if (!ret.ContainsKey(k))
                            {
                                ret.Add(k, Math.Max(cognateScore, teScore));
                            }
                            toRemove1.Add(key);
                            toRemove2.Add(key2);
                        }
                    }
                }

                foreach (string rem in toRemove1)
                {
                    nesSrc.Remove(rem);
                }
                foreach (string rem in toRemove2)
                {
                    nesTrg.Remove(rem);
                }
            }

            //different forms, different type
            foreach (string key in nesSrc.Keys.ToArray())
            {
                HashSet<string> toRemove1 = new HashSet<string>();
                HashSet<string> toRemove2 = new HashSet<string>();

                foreach (string key2 in nesTrg.Keys.ToArray())
                {
                    double cognateScore = computeCognateScore(key, key2);
                    double teScore = computeTeScore(key, key2, teModel);

                    if (cognateScore >= threshold)
                    {
                        string k = key + "\t" + key2;
                        if (!ret.ContainsKey(k))
                        {
                            ret.Add(k, Math.Max(cognateScore, teScore));
                        }
                        toRemove1.Add(key);
                        toRemove2.Add(key2);
                    }
                    else if (teScore >= threshold)
                    {
                        string k = key + "\t" + key2;
                        if (!ret.ContainsKey(k))
                        {
                            ret.Add(k, Math.Max(cognateScore, teScore));
                        }
                        toRemove1.Add(key);
                        toRemove2.Add(key2);
                    }
                }

                foreach (string rem in toRemove1)
                {
                    nesSrc.Remove(rem);
                }
                foreach (string rem in toRemove2)
                {
                    nesTrg.Remove(rem);
                }
            }
        }

        private static double computeTeScore(string key, string key2, Dictionary<string, Dictionary<string, double>> teModel)
        {
            string[] parts1 = key.Split(' ');
            string[] parts2 = key2.Split(' ');

            double total = 0;

            foreach (string s in parts1)
            {
                double max = 0;
                if (teModel.ContainsKey(s))
                {
                    foreach (string t in parts2)
                    {
                        if (teModel[s].ContainsKey(t))
                        {
                            if (teModel[s][t] > max)
                            {
                                max = teModel[s][t];
                            }
                        }
                    }
                }
                total += max;
            }

            double adaos = Math.Abs((double)parts1.Length - (double)parts2.Length) / 2.0;
            return total / ((double)parts1.Length + adaos);
        }

        private static string removeDoubles(string text2)
        {
            StringBuilder sb = new StringBuilder();
            string prev = "";
            for (int i = 0; i < text2.Length; i++)
            {
                string currentLetter = text2.Substring(i, 1);
                if (currentLetter != prev)
                {
                    sb.Append(currentLetter);
                    prev = currentLetter;
                }
            }
            return sb.ToString();
        }

        private static int LevenshteinDistance(String s, String t)
        {
            int ret = 0;

            if (s == null || t == null)
            {
                Console.WriteLine("Strings must not be null");
            }

            int n = s.Length; // length of s
            int m = t.Length; // length of t

            if (n == 0)
            {
                ret = m;
            }
            else if (m == 0)
            {
                ret = n;
            }
            else
            {
                int[] p = new int[n + 1]; // 'previous' cost array, horizontally
                int[] d = new int[n + 1]; // cost array, horizontally
                int[] _d; // placeholder to assist in swapping p and d

                // indexes into strings s and t
                int i; // iterates through s
                int j; // iterates through t

                char t_j; // jth character of t

                int cost; // cost

                for (i = 0; i <= n; i++)
                {
                    p[i] = i;
                }

                for (j = 1; j <= m; j++)
                {
                    t_j = t[j - 1];
                    d[0] = j;

                    for (i = 1; i <= n; i++)
                    {
                        cost = s[i - 1] == t_j ? 0 : 1;
                        // minimum of cell to the left+1, to the top+1, diagonally
                        // left
                        // and up +cost
                        d[i] = Math.Min(Math.Min(d[i - 1] + 1, p[i] + 1), p[i - 1]
                                + cost);
                    }

                    // copy current distance counts to 'previous row' distance
                    // counts
                    _d = p;
                    p = d;
                    d = _d;
                }

                // our last action in the above loop was to switch d and p, so p now
                // actually has the most recent cost counts
                ret = p[n];
            }

            return ret;

        } //int computeLevenshteinDistance(String s, String t)

        private static bool detectInversion(string text1Var, string text2Var)
        {
            if (text1Var.Length >= 2)
            {
                for (int i = 0; i < text1Var.Length - 1; i++)
                {
                    string newVal = text1Var.Substring(0, i) + text1Var.Substring(i + 1, 1) + text1Var.Substring(i, 1) + text1Var.Substring(i + 2);
                    if (newVal == text2Var)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static double computeCognateScore(string text1, string text2)
        {
            text1 = text1.ToLower();
            text2 = text2.ToLower();

            if (text1 != "" && text2 != "" && text1.Substring(0, 1) == text2.Substring(0, 1))
            {
                string text1Var = text1.Replace("ph", "f").Replace("y", "i").Replace("hn", "n").Replace("ha", "a"); ;
                string text2Var = text2.Replace("ph", "f").Replace("y", "i").Replace("hn", "n").Replace("ha", "a"); ;
                string text1VarNoDouble = text1Var;
                string text2VarNoDouble = text2Var;
                text1Var = removeDoubles(text1Var);
                text2Var = removeDoubles(text2Var);

                int distance = LevenshteinDistance(text1VarNoDouble, text2VarNoDouble) + 1;

                //double score = (1.0 / Math.Pow((double)distance2, 1.0 / 2));
                double score = 1.0 - (double)distance / Math.Min((double)text1.Length + 1, (double)text2.Length + 1);
                double max = Math.Min((double)text1.Length, (double)text2.Length);
                if (max > 5)
                {
                    double lcs = (double)longestCommonSubstring(text1, text2) / max;
                    score = (score + lcs) / 2.0;
                }

                //if (distanceOrg > 3 && text1.Length < 8 && text2.Length < 8)
                //{
                //    score = score / distanceOrg;
                //}
                return score;
            }
            return 0;
        }

        private static int longestCommonSubstring(string str1, string str2)
        {
            if (String.IsNullOrEmpty(str1) || String.IsNullOrEmpty(str2))
                return 0;

            int[,] num = new int[str1.Length, str2.Length];
            int maxlen = 0;

            for (int i = 0; i < str1.Length; i++)
            {
                for (int j = 0; j < str2.Length; j++)
                {
                    if (str1[i] != str2[j])
                        num[i, j] = 0;
                    else
                    {
                        if ((i == 0) || (j == 0))
                            num[i, j] = 1;
                        else
                            num[i, j] = 1 + num[i - 1, j - 1];

                        if (num[i, j] > maxlen)
                        {
                            maxlen = num[i, j];
                        }
                    }
                }
            }
            return maxlen;
        }

        private static Dictionary<string, Dictionary<string, int>> count(List<Entity> srcNES)
        {
            Dictionary<string, Dictionary<string, int>> ret = new Dictionary<string, Dictionary<string, int>>();

            foreach (Entity ent in srcNES)
            {
                string occ = ent.occurence.ToLower();
                string type = ent.type;

                if (!ret.ContainsKey(occ))
                {
                    ret.Add(occ, new Dictionary<string, int>());
                }

                if (!ret[occ].ContainsKey(type))
                {
                    ret[occ].Add(type, 1);
                }
                else
                {
                    ret[occ][type]++;
                }
            }

            return ret;
        }

        private static Dictionary<string, Dictionary<string, double>> readTE(string teModelFileName)
        {
            Dictionary<string, Dictionary<string, double>> ret = new Dictionary<string, Dictionary<string, double>>();
            CultureInfo ci = CultureInfo.InvariantCulture;

            StreamReader rdr = new StreamReader(teModelFileName, Encoding.UTF8);
            string line = "";
            while ((line = rdr.ReadLine()) != null)
            {
                line = line.Trim();
                string[] parts = line.Split('\t');
                if (parts.Length == 3)
                {
                    if (!ret.ContainsKey(parts[0]))
                    {
                        ret.Add(parts[0], new Dictionary<string, double>());
                    }

                    if (!ret[parts[0]].ContainsKey(parts[1]))
                    {
                        ret[parts[0]].Add(parts[1], double.Parse(parts[2], ci));
                    }
                }
            }
            rdr.Close();

            return ret;
        }

        private static void readNamedEntities(string fileIn, string fileOut, ref List<Entity> srcNES, ref List<Entity> trgNES, bool additionalAnnotation)
        {
            readNamedEntities(fileIn, ref srcNES, additionalAnnotation);
            readNamedEntities(fileOut, ref trgNES, additionalAnnotation);
        }

        private static void readNamedEntities(string file, ref List<Entity> nes, bool additionalAnnotation)
        {
            string text = DataStructReader.readWholeTextFile(file, Encoding.UTF8);

            Regex regex = new Regex(
                  "<.+?MEX TYPE=\"(?<type>.+?)\">(?<occ>.+?)</.+?MEX>",
                RegexOptions.Singleline
                );

            Match m = regex.Match(text);

            while (m.Success)
            {
                Entity ent = new Entity();
                ent.type = m.Groups["type"].Value;
                if (!additionalAnnotation)
                {
                    ent.occurence = m.Groups["occ"].Value;
                }
                else
                {
                    ent.occurence = stripAnnotation(m.Groups["occ"].Value);
                }
                nes.Add(ent);
                m = m.NextMatch();
            }
        }

        private static string stripAnnotation(string text)
        {
            Regex regex = new Regex(
                  "<.+?>",
                RegexOptions.None);

            return regex.Replace(text, "");
        }
    }
}
