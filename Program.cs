using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;

//namespace Buff(BMEngine) is coded by Arduano
//https://github.com/arduano/Zenith-MIDI/blob/master/BMEngine/BufferByteReader.cs
//I was allowed by Arduano to use this namespace
namespace Buff
{
    public class BufferByteReader
    {
        long pos;
        int buffersize;
        int bufferpos;
        int maxbufferpos;
        long streamstart;
        long streamlen;
        Stream stream;
        byte[] buffer;
        byte[] bufferNext;
        Task nextReader = null;

        public BufferByteReader(Stream stream, int buffersize, long streamstart, long streamlen)
        {
            if (buffersize > streamlen) buffersize = (int)streamlen;
            this.buffersize = buffersize;
            this.streamstart = streamstart;
            this.streamlen = streamlen;
            this.stream = stream;
            buffer = new byte[buffersize];
            bufferNext = new byte[buffersize];
            UpdateBuffer(pos, true);
        }

        void UpdateBuffer(long pos, bool first = false)
        {
            if (first)
            {
                nextReader = Task.Run(() =>
                {
                    lock (stream)
                    {
                        stream.Position = pos + streamstart;
                        stream.Read(bufferNext, 0, buffersize);
                    }
                });
            }
            nextReader.GetAwaiter().GetResult();
            Buffer.BlockCopy(bufferNext, 0, buffer, 0, buffersize);
            nextReader = Task.Run(() =>
            {
                lock (stream)
                {
                    stream.Position = pos + streamstart + buffersize;
                    stream.Read(bufferNext, 0, buffersize);
                }
            });
            nextReader.GetAwaiter().GetResult();
            //lock (stream)
            //{
            //    stream.Position = pos + streamstart;
            //    stream.Read(buffer, 0, buffersize);
            //}
            maxbufferpos = (int)Math.Min(streamlen - pos + 1, buffersize);
        }

        public long Location => pos;

        public int Pushback { get; set; } = -1;

        public byte Read()
        {
            if (Pushback != -1)
            {
                byte _b = (byte)Pushback;
                Pushback = -1;
                return _b;
            }
            byte b = buffer[bufferpos++];
            if (bufferpos < maxbufferpos) return b;
            else if (bufferpos >= buffersize)
            {
                pos += bufferpos;
                bufferpos = 0;
                UpdateBuffer(pos);
                return b;
            }
            else throw new IndexOutOfRangeException();
        }

        public byte ReadFast()
        {
            byte b = buffer[bufferpos++];
            if (bufferpos < maxbufferpos) return b;
            else if (bufferpos >= buffersize)
            {
                pos += bufferpos;
                bufferpos = 0;
                UpdateBuffer(pos);
                return b;
            }
            else throw new IndexOutOfRangeException();
        }

        public void Reset()
        {
            pos = 0;
            bufferpos = 0;
            UpdateBuffer(pos, true);
        }

        public void Skip(int count)
        {
            for (int i = 0; i < count; i++)
            {
                bufferpos++;
                if (bufferpos < maxbufferpos) continue;
                if (bufferpos >= buffersize)
                {
                    pos += bufferpos;
                    bufferpos = 0;
                    UpdateBuffer(pos);
                }
                else throw new IndexOutOfRangeException();
            }
        }

        public void Dispose()
        {
            buffer = null;
        }
    }
}

namespace Midi_Counter_Generator
{
    struct pairli
    {
        public long x;
        public double y;
        public int trk, cnt;
        public pairli(long a, double b, int c, int d)
        {
            x = a;
            y = b;
            trk = c;
            cnt = d;
        }
        public static bool operator <(pairli fx, pairli fy)
        {
            if (fx.x != fy.x)
            {
                return fx.x < fy.x;
            }
            else if (fx.trk != fy.trk)
            {
                return fx.trk < fy.trk;
            }
            else if (fx.cnt != fy.cnt)
            {
                return fx.cnt < fy.cnt;
            }
            else
            {
                return false;
            }
        }
        public static bool operator >(pairli fx, pairli fy)
        {
            if (fx.x != fy.x)
            {
                return fx.x > fy.x;
            }
            else if (fx.trk != fy.trk)
            {
                return fx.trk > fy.trk;
            }
            else if (fx.cnt != fy.cnt)
            {
                return fx.cnt > fy.cnt;
            }
            else
            {
                return false;
            }
        }
    }
    class mainpart
    {
        public string filein, fileout;
        int isfast;
        BufferedStream inp;
        int fps;
        long[] pos;
        string pat;
        long alltic = 0;
        int delay;
        static int resol;
        int toint(int x)
        {
            return x < 0 ? x + 256 : x;
        }
        public void read()
        {
            Console.WriteLine("Note: There should be an ffmpeg.exe in Counter's folder.");
            string sss = "";
            Console.Write("Input MIDI filename: ");
            filein = Console.ReadLine();
            if (filein[0] == '\"')
            {
                string file1n = "";
                for (int i = 1; i < filein.Length - 1; i++)
                {
                    file1n += filein[i];
                }
                filein = file1n;
            }
            Console.Write("Input video filename (Default: MIDIname+.counter.mov): ");
            fileout = (sss = Console.ReadLine()) == "" ? filein + ".counter.mov" : sss;
            if (fileout[0] == '\"')
            {
                string file1n = "";
                for (int i = 1; i < fileout.Length - 1; i++)
                {
                    file1n += fileout[i];
                }
                fileout = file1n;
            }
            Console.Write("Input video fps (Default: 25): ");
            fps = Convert.ToInt32((sss = Console.ReadLine()) == "" ? "25" : sss);
            Console.Write("Input delay start seconds (Default: 3): ");
            delay = Convert.ToInt32((sss = Console.ReadLine()) == "" ? "3" : sss);
            delay *= fps;
            Console.Write("Do you want to use fast render? (WARNING: fast render will use lots of RAM)(Input 1 if the answer is YES, 0 otherwise, default: 0): ");
            isfast = Convert.ToInt32((sss = Console.ReadLine()) == "" ? "0" : sss);
            Console.WriteLine("------------------------------------------");
            Console.WriteLine("If you want to know how to edit patterns, please read README.txt in the Patterns folder.");
            Console.Write("Input pattern ID (Default: 0): ");
            pat = (sss = Console.ReadLine()) == "" ? "0" : sss;
        }
        void ReportError()
        {
            Console.WriteLine("File Error. Check if it is a correct MIDI file.");
            Console.ReadKey();
        }
        struct Patterns
        {
            public int W, H;
            public string fon;
            public int fontsz;
            public string patterns;

            public void readpattern(string id)
            {
                string fol = "Patterns\\" + id + ".txt";
                StreamReader inpp = new StreamReader(fol);
                W = Convert.ToInt32(inpp.ReadLine());
                H = Convert.ToInt32(inpp.ReadLine());
                fon = inpp.ReadLine();
                fontsz = Convert.ToInt32(inpp.ReadLine());
                patterns = "";
                while (true)
                {
                    string s = inpp.ReadLine();
                    if (s == null)
                    {
                        break;
                    }
                    patterns += s;
                    patterns += "\n";
                }
            }
            public string tocom(long s)
            {
                string S = Convert.ToString(s);
                string SS = "";
                for (int i = S.Length - 1; i >= 0; i--)
                {
                    if ((S.Length - i) % 3 == 0 && i != 0 && S[i - 1] != '-')
                    {
                        SS = "," + S[i] + SS;
                    }
                    else
                    {
                        SS = S[i] + SS;
                    }
                }
                return SS;
            }
            public string getstring(long nc, long an, double bp, double tm, long np, long po, long ti, long at)
            {
                string ss = patterns;
                ss = ss.Replace("{0}", Convert.ToString(nc));
                ss = ss.Replace("{0,}", tocom(nc));
                ss = ss.Replace("{1}", Convert.ToString(an));
                ss = ss.Replace("{1,}", tocom(an));
                ss = ss.Replace("{1-0}", Convert.ToString(an - nc));
                ss = ss.Replace("{1-0,}", tocom(an - nc));
                ss = ss.Replace("{2}", Convert.ToString(Math.Round(bp * 10) / 10));
                ss = ss.Replace("{3}", Convert.ToString(Math.Round(tm * 100) / 100));
                ss = ss.Replace("{4}", Convert.ToString(np));
                ss = ss.Replace("{4,}", tocom(np));
                ss = ss.Replace("{5}", Convert.ToString(po));
                ss = ss.Replace("{5,}", tocom(po));
                ss = ss.Replace("{6}", Convert.ToString(ti));
                ss = ss.Replace("{6,}", tocom(ti));
                ss = ss.Replace("{7}", Convert.ToString(at));
                ss = ss.Replace("{7,}", tocom(at));
                ss = ss.Replace("{7-6}", Convert.ToString(at - ti));
                ss = ss.Replace("{7-6,}", tocom(at - ti));
                ss = ss.Replace("{8}", Convert.ToString(ti / resol + 1));
                ss = ss.Replace("{9}", Convert.ToString(at / resol + 1));
                ss = ss.Replace("{9-8}", Convert.ToString(at / resol - ti / resol));
                ss = ss.Replace("{A}", Convert.ToString(resol));
                ss = ss.Replace("{A,}", tocom(resol));
                return ss;
            }
            public void draw(long nc, long an, double bp, double tm, long np, long po, long ti, long at, int id, string tmpfol)
            {
                using (var font = new Font(fon, fontsz))
                {
                    var image = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var gfx = Graphics.FromImage(image))
                    {
                        gfx.FillRectangle(Brushes.Transparent, 0, 0, image.Width, image.Height);

                        using (var textBrush = new SolidBrush(Color.White))
                        {
                            gfx.DrawString(getstring(nc, an, bp, tm, np, po, ti, at), font, textBrush, new PointF(0, 0));
                        }
                    }
                    image.Save(tmpfol + id + ".png");
                    image.Dispose();
                }
            }
        }
        Patterns pts;
        Process startNewFF(string inp, string path)
        {
            Process ffmpeg = new Process();
            string args = "" +
                    " -f image2" +
                    " -r " + Convert.ToString(fps) + " -i " + inp;
            args += " -y \"" + path + "\"";
            Console.WriteLine(args);
            ffmpeg.StartInfo = new ProcessStartInfo("ffmpeg", args);
            ffmpeg.StartInfo.RedirectStandardInput = true;
            ffmpeg.StartInfo.UseShellExecute = false;
            ffmpeg.StartInfo.RedirectStandardError = false;
            try
            {
                ffmpeg.Start();
                ffmpeg.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}\nThere was an error starting the ffmpeg process\nIs ffmpeg.exe in the same folder as this program?", ex.Message);
            }
            return ffmpeg;
        }
        void OriginalRender()
        {
            inp = new BufferedStream(File.Open(filein, FileMode.Open, FileAccess.Read, FileShare.Read), 64);
            for (int i = 0; i < 4; ++i)
            {
                inp.ReadByte();
            }
            for (int i = 0; i < 4; ++i)
            {
                inp.ReadByte();
            }
            inp.ReadByte();
            inp.ReadByte();
            int trkcnt;
            trkcnt = (toint(inp.ReadByte()) * 256) + toint(inp.ReadByte());
            Console.WriteLine("Track Count: {0}", trkcnt);
            resol = (toint(inp.ReadByte()) * 256) + toint(inp.ReadByte());
            Console.WriteLine("Positioning the beginning of each track...");
            pos = new long[trkcnt];
            long[] leng = new long[trkcnt];
            long cntpos = 14;
            for (int i = 0; i < trkcnt; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    toint(inp.ReadByte());
                    cntpos++;
                }
                long len = toint(inp.ReadByte());
                len = len * 256 + toint(inp.ReadByte());
                len = len * 256 + toint(inp.ReadByte());
                len = len * 256 + toint(inp.ReadByte());
                leng[i] = len;
                pos[i] = cntpos + 4;
                cntpos += 4 + len;
                Console.WriteLine("Positioned track {0}/{1}, Size {2}", i + 1, trkcnt, len);
                inp.Seek(cntpos, SeekOrigin.Begin);
            }
            inp.Close();
            ArrayList bpm = new ArrayList();
            bpm.Add(new pairli(0, 500000 / resol, 0, 0));
            long noteall = 0;
            int nowtrk = 1;
            Parallel.For(0, trkcnt, trk =>
            {
                int bpmcnt = 0;
                long notes = 0;
                Buff.BufferByteReader tr = new Buff.BufferByteReader(File.Open(filein, FileMode.Open, FileAccess.Read, FileShare.Read), 1048576, pos[trk], leng[trk]);
                Console.WriteLine("Reading track {0}({1})/{2}, Size {3}, Using thread {4}", nowtrk, trk + 1, trkcnt, leng[trk], Thread.CurrentThread.ManagedThreadId);
                long getnum()
                {
                    long ans = 0;
                    int ch = 256;
                    while (ch >= 128)
                    {
                        ch = toint(tr.ReadFast());
                        ans = ans * 128 + (ch & 0b01111111);
                    }
                    return ans;
                }
                long TM = 0;
                int prvcmd = 256;
                while (true)
                {
                    TM += getnum();
                    int cmd = tr.ReadFast();
                    if (cmd < 128)
                    {
                        tr.Pushback = cmd;
                        cmd = prvcmd;
                    }
                    prvcmd = cmd;
                    int cm = cmd & 0b11110000;
                    if (cm == 0b10010000)
                    {
                        tr.Read();
                        tr.Skip(1);
                        notes++;
                    }
                    else if (cm == 0b10000000)
                    {
                        tr.Read();
                        tr.Skip(1);
                    }
                    else if (cm == 0b11000000 || cm == 0b11010000 || cmd == 0b11110011)
                    {
                        tr.Read();
                    }
                    else if (cm == 0b11100000 || cm == 0b10110000 || cmd == 0b11110010 || cm == 0b10100000)
                    {
                        tr.Read();
                        tr.Skip(1);
                    }
                    else if (cmd == 0b11110000)
                    {
                        if (tr.Read() == 0b11110111)
                        {
                            continue;
                        }
                        do
                        {
                        } while (tr.ReadFast() != 0b11110111);
                    }
                    else if (cmd == 0b11110100 || cmd == 0b11110001 || cmd == 0b11110101 || cmd == 0b11111001 || cmd == 0b11111101 || cmd == 0b11110110 || cmd == 0b11110111 || cmd == 0b11111000 || cmd == 0b11111010 || cmd == 0b11111100 || cmd == 0b11111110)
                    {
                    }
                    else if (cmd == 0b11111111)
                    {
                        cmd = tr.Read();
                        if (cmd == 0)
                        {
                            tr.Skip(1);
                        }
                        else if (cmd >= 1 && cmd <= 10 || cmd == 0x7f)
                        {
                            long ff = getnum();
                            tr.Skip(Convert.ToInt32(ff));
                        }
                        else if (cmd == 0x20 || cmd == 0x21)
                        {
                            tr.Skip(2);
                        }
                        else if (cmd == 0x2f)
                        {
                            tr.Skip(1);
                            break;
                        }
                        else if (cmd == 0x51)
                        {
                            bpmcnt++;
                            tr.Skip(1);
                            int BPM = toint(tr.ReadFast());
                            BPM = BPM * 256 + toint(tr.ReadFast());
                            BPM = BPM * 256 + toint(tr.ReadFast());
                            lock (inp)
                            {
                                bpm.Add(new pairli(TM, 1.0 * BPM / resol, trk, bpmcnt));
                            }
                        }
                        else if (cmd == 0x54 || cmd == 0x58)
                        {
                            tr.Skip(5);
                        }
                        else if (cmd == 0x59)
                        {
                            tr.Skip(3);
                        }
                        else if (cmd == 0x0a)
                        {
                            int ss = toint(tr.ReadFast());
                            tr.Skip(ss);
                        }
                    }
                }
                lock (inp)
                {
                    if (TM > alltic)
                    {
                        alltic = TM;
                    }
                }
                lock (inp)
                {
                    noteall += notes;
                }
                nowtrk++;
                tr.Dispose();
            });
            Console.WriteLine("Reading finished. Note count: {0}", noteall);
            Console.WriteLine("Sorting tempo events");
            for (int i = 0; i < bpm.Count; i++)
            {
                for (int j = i + 1; j < bpm.Count; j++)
                {
                    if (((pairli)bpm[i]) > ((pairli)bpm[j]))
                    {
                        pairli xx = (pairli)bpm[i];
                        bpm[j] = bpm[i];
                        bpm[i] = xx;
                    }
                }
            }
            Console.WriteLine("Generating time for bpm events...");
            double[] tmc = new double[bpm.Count];
            tmc[0] = 0;
            for (int i = 1; i < bpm.Count; i++)
            {
                //Console.WriteLine("{0} {1} {2} {3}",((pairli)bpm[i]).x,((pairli)bpm[i - 1]).x,((pairli)bpm[i - 1]).y,resol);
                tmc[i] = tmc[i - 1] + (((pairli)bpm[i]).x - ((pairli)bpm[i - 1]).x) * ((pairli)bpm[i - 1]).y;
            }
            Console.WriteLine("Reading pattern...");
            pts.readpattern(pat);
            Console.WriteLine("Generating temp folder");
            string tmpf;
            long tohex(string s)
            {
                long ha1 = 0, ha2 = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    ha1 = ha1 * 277 + s[i];
                    ha1 %= 1000000007;
                    ha2 = ha2 * 1003 + s[i];
                    ha2 %= 1000000009;
                }
                return ha1 * 1000000009 + ha2;
            }
            string HexNum = "0123456789ABCDEF";
            string tofile(string s)
            {
                long x = tohex(s);
                string ans = "";
                while (x > 0)
                {
                    ans += HexNum[Convert.ToInt32(x % 16)];
                    x /= 16;
                }
                ans = "C:\\Users\\Public\\~$MidiCounterGen" + ans;
                return ans;
            }
            tmpf = tofile(filein) + "\\";
            Directory.CreateDirectory(tmpf);
            int faq = trkcnt;
            int bpmptr = 0;
            bool[] died = new bool[trkcnt];
            int[] prec = new int[trkcnt];
            long[] tms = new long[trkcnt];
            double tmm = 0;
            long notecnt = 0;
            int tmdf = 0;
            for (int i = 0; i < delay; i++)
            {
                pts.draw(0, noteall, 120, 0, 0, 0, 0, alltic, tmdf, tmpf);
                Console.WriteLine("Generated frame {0}, 0 notes.", tmdf);
                tmdf++;
            }
            long tmnow = 0;
            long[] history = new long[fps];
            long poly = 0;
            inp = new BufferedStream(File.Open(filein, FileMode.Open, FileAccess.Read, FileShare.Read), 16384);
            while (faq > 0)
            {
                for (int i = 0; i < trkcnt; i++)
                {
                    if (!died[i])
                    {
                        inp.Seek(pos[i], SeekOrigin.Begin);
                        int lstcmd = 256;
                        int go()
                        {
                            if (lstcmd != 256)
                            {
                                int lstcmd2 = lstcmd;
                                lstcmd = 256;
                                return lstcmd2;
                            }
                            int x = inp.ReadByte();
                            return x < 0 ? x + 256 : x;
                        }
                        long Go()
                        {
                            long x = 0;
                            int c;
                            do
                            {
                                c = go();
                                x = x * 128 + (c & 0b01111111);
                            } while (c >= 128);
                            return x;
                        }
                        bool bg = false;
                        while (true)
                        {
                            if (bg)
                            {
                                tms[i] += Go();
                            }
                            else
                            {
                                bg = true;
                            }
                            if (tms[i] > tmm)
                            {
                                pos[i] = inp.Position;
                                break;
                            }
                            int cmd = go();
                            if (cmd < 128)
                            {
                                lstcmd = cmd;
                                cmd = prec[i];
                            }
                            prec[i] = cmd;
                            int cm = cmd & 0b11110000;
                            if (cm == 0b10010000)
                            {
                                go();
                                go();
                                notecnt++;
                                poly++;
                            }
                            else if (cm == 0b10000000)
                            {
                                go(); go();
                                poly--;
                            }
                            else if (cm == 0b11000000 || cm == 0b11010000 || cmd == 0b11110011)
                            {
                                go();
                            }
                            else if (cm == 0b11100000 || cm == 0b10110000 || cmd == 0b11110010 || cm == 0b10100000)
                            {
                                go(); go();
                            }
                            else if (cmd == 0b11110000)
                            {
                                if (go() == 0b11110111)
                                {
                                    continue;
                                }
                                do
                                {
                                } while (go() != 0b11110111);
                            }
                            else if (cmd == 0b11110100 || cmd == 0b11110001 || cmd == 0b11110101 || cmd == 0b11111001 || cmd == 0b11111101 || cmd == 0b11110110 || cmd == 0b11110111 || cmd == 0b11111000 || cmd == 0b11111010 || cmd == 0b11111100 || cmd == 0b11111110)
                            {
                            }
                            else if (cmd == 0b11111111)
                            {
                                cmd = go();
                                if (cmd == 0)
                                {
                                    go();
                                }
                                else if (cmd >= 1 && cmd <= 10 || cmd == 0x7f)
                                {
                                    long ff = Go();
                                    while ((ff--) > 0)
                                    {
                                        go();
                                    }
                                }
                                else if (cmd == 0x20 || cmd == 0x21)
                                {
                                    go(); go();
                                }
                                else if (cmd == 0x2f)
                                {
                                    go();
                                    faq--;
                                    died[i] = true;
                                    break;
                                }
                                else if (cmd == 0x51)
                                {
                                    go(); go(); go(); go();
                                }
                                else if (cmd == 0x54 || cmd == 0x58)
                                {
                                    go(); go(); go(); go(); go();
                                }
                                else if (cmd == 0x59)
                                {
                                    go(); go(); go();
                                }
                                else if (cmd == 0x0a)
                                {
                                    int ss = toint(go());
                                    while ((ss--) > 0)
                                    {
                                        go();
                                    }
                                }
                            }
                        }
                    }
                }
                pts.draw(notecnt, noteall, 60000000.0 / resol / ((pairli)bpm[bpmptr]).y, 1.0 * (tmdf - delay) / fps, notecnt - history[tmdf % fps], poly, tmm > alltic ? alltic : Convert.ToInt64(tmm), alltic, tmdf, tmpf);
                history[tmdf % fps] = notecnt;
                tmdf++;
                tmnow = Convert.ToInt64((tmdf - delay) * 1000000.0 / fps);
                //Console.WriteLine("{0} {1}", tmc[bpmptr + 1], tmnow);
                while (bpmptr < bpm.Count - 1 && tmc[bpmptr + 1] < Convert.ToDouble(tmnow))
                {
                    bpmptr++;
                }
                tmm = Convert.ToInt64(((pairli)bpm[bpmptr]).x + (tmnow - tmc[bpmptr]) / ((pairli)bpm[bpmptr]).y);
                Console.WriteLine("Generated frame {0}, {1} notes.", tmdf - 1, notecnt);
            }
            for (int i = 0; i < 5 * fps; i++)
            {
                pts.draw(noteall, noteall, 60000000.0 / resol / ((pairli)bpm[bpmptr]).y, 1.0 * (tmdf - delay) / fps, notecnt - history[tmdf % fps], 0, alltic, alltic, tmdf, tmpf);
                history[tmdf % fps] = noteall;
                tmdf++;
                Console.WriteLine("Generated frame {0}, {1} notes.", tmdf - 1, noteall);
            }
            Console.WriteLine("Generating video ...");
            startNewFF(tmpf + "%d.png", fileout);
            Console.WriteLine("Cleaning...");
            Directory.Delete(tmpf, true);
            Console.WriteLine("Converting finished. Press any key to exit...");
            Console.ReadKey();
        }
        void FastRender()
        {
            inp = new BufferedStream(File.Open(filein, FileMode.Open, FileAccess.Read, FileShare.Read), 1048576);
            for (int i = 0; i < 4; ++i)
            {
                inp.ReadByte();
            }
            for (int i = 0; i < 4; ++i)
            {
                inp.ReadByte();
            }
            inp.ReadByte();
            inp.ReadByte();
            int trkcnt;
            trkcnt = (toint(inp.ReadByte()) * 256) + toint(inp.ReadByte());
            Console.WriteLine("Track Count: {0}", trkcnt);
            resol = (toint(inp.ReadByte()) * 256) + toint(inp.ReadByte());
            ArrayList bpm = new ArrayList();
            bpm.Add(new pairli(0, 500000 / resol, 0, 0));
            long noteall = 0;
            int nowtrk = 1;
            ArrayList nts = new ArrayList(), nto = new ArrayList();
            for (int trk = 0; trk < trkcnt; trk++)
            {
                int bpmcnt = 0;
                long notes = 0;
                long leng = 0;
                inp.ReadByte();
                inp.ReadByte();
                inp.ReadByte();
                inp.ReadByte();
                for(int i = 0; i < 4; i++)
                {
                    leng = leng * 256 + toint(inp.ReadByte());
                }
                int lstcmd = 256;
                Console.WriteLine("Reading track {1}/{2}, Size {3}", nowtrk, trk + 1, trkcnt, leng);
                int getnum()
                {
                    int ans = 0;
                    int ch = 256;
                    while (ch >= 128)
                    {
                        ch = toint(inp.ReadByte());
                        leng--;
                        ans = ans * 128 + (ch & 0b01111111);
                    }
                    return ans;
                }
                int get()
                {
                    if (lstcmd != 256)
                    {
                        int lstcmd2 = lstcmd;
                        lstcmd = 256;
                        return lstcmd2;
                    }
                    leng--;
                    return toint(inp.ReadByte());
                }
                int TM = 0;
                int prvcmd = 256;
                while (true)
                {
                    TM += getnum();
                    int cmd = inp.ReadByte();
                    leng--;
                    if (cmd < 128)
                    {
                        lstcmd = cmd;
                        cmd = prvcmd;
                    }
                    prvcmd = cmd;
                    int cm = cmd & 0b11110000;
                    if (cm == 0b10010000)
                    {
                        get();
                        inp.ReadByte();
                        leng--;
                        lock (inp)
                        {
                            while (nts.Count <= TM)
                            {
                                nts.Add(0L);
                            }
                            nts[TM] = (Convert.ToInt64(nts[TM]) + 1L);
                        }
                        notes++;
                    }
                    else if (cm == 0b10000000)
                    {
                        get();
                        inp.ReadByte();
                        leng--;
                        lock (inp)
                        {
                            while (nto.Count <= TM)
                            {
                                nto.Add(0L);
                            }
                            nto[TM] = (Convert.ToInt64(nto[TM]) + 1L);
                        }
                    }
                    else if (cm == 0b11000000 || cm == 0b11010000 || cmd == 0b11110011)
                    {
                        get();
                    }
                    else if (cm == 0b11100000 || cm == 0b10110000 || cmd == 0b11110010 || cm == 0b10100000)
                    {
                        get();
                        inp.ReadByte();
                        leng--;
                    }
                    else if (cmd == 0b11110000)
                    {
                        if (get() == 0b11110111)
                        {
                            continue;
                        }
                        do
                        {
                            leng--;
                        } while (inp.ReadByte() != 0b11110111);
                    }
                    else if (cmd == 0b11110100 || cmd == 0b11110001 || cmd == 0b11110101 || cmd == 0b11111001 || cmd == 0b11111101 || cmd == 0b11110110 || cmd == 0b11110111 || cmd == 0b11111000 || cmd == 0b11111010 || cmd == 0b11111100 || cmd == 0b11111110)
                    {
                    }
                    else if (cmd == 0b11111111)
                    {
                        cmd = get();
                        if (cmd == 0)
                        {
                            inp.ReadByte();
                            leng--;
                        }
                        else if (cmd >= 1 && cmd <= 10 || cmd == 0x7f)
                        {
                            long ff = getnum();
                            while (ff-- > 0)
                            {
                                inp.ReadByte();
                                leng--;
                            }
                        }
                        else if (cmd == 0x20 || cmd == 0x21)
                        {
                            inp.ReadByte(); inp.ReadByte(); leng -= 2;
                        }
                        else if (cmd == 0x2f)
                        {
                            inp.ReadByte();
                            leng--;
                            break;
                        }
                        else if (cmd == 0x51)
                        {
                            bpmcnt++;
                            inp.ReadByte();
                            leng--;
                            int BPM = get();
                            BPM = BPM * 256 + get();
                            BPM = BPM * 256 + get();
                            lock (inp)
                            {
                                bpm.Add(new pairli(TM, 1.0 * BPM / resol, trk, bpmcnt));
                            }
                        }
                        else if (cmd == 0x54 || cmd == 0x58)
                        {
                            inp.ReadByte(); inp.ReadByte(); inp.ReadByte(); inp.ReadByte(); inp.ReadByte();
                            leng -= 5;
                        }
                        else if (cmd == 0x59)
                        {
                            inp.ReadByte(); inp.ReadByte(); inp.ReadByte();
                            leng -= 3;
                        }
                        else if (cmd == 0x0a)
                        {
                            int ss = get();
                            while (ss-- > 0)
                            {
                                inp.ReadByte();
                                leng--;
                            }
                        }
                    }
                }
                while (leng > 0)
                {
                    inp.ReadByte();
                    leng--;
                }
                if (TM > alltic)
                {
                    alltic = TM;
                }
                while (nts.Count <= TM)
                {
                    nts.Add(0);
                }
                while (nto.Count <= TM)
                {
                    nto.Add(0);
                }
                noteall += notes;
                nowtrk++;
            }
            Console.WriteLine("Reading finished. Note count: {0}", noteall);
            Console.WriteLine("Sorting tempo events");
            for (int i = 0; i < bpm.Count; i++)
            {
                for (int j = i + 1; j < bpm.Count; j++)
                {
                    if (((pairli)bpm[i]) > ((pairli)bpm[j]))
                    {
                        pairli xx = (pairli)bpm[i];
                        bpm[j] = bpm[i];
                        bpm[i] = xx;
                    }
                }
            }
            Console.WriteLine("Generating time for bpm events...");
            double[] tmc = new double[bpm.Count];
            tmc[0] = 0;
            for (int i = 1; i < bpm.Count; i++)
            {
                //Console.WriteLine("{0} {1} {2} {3}",((pairli)bpm[i]).x,((pairli)bpm[i - 1]).x,((pairli)bpm[i - 1]).y,resol);
                tmc[i] = tmc[i - 1] + (((pairli)bpm[i]).x - ((pairli)bpm[i - 1]).x) * ((pairli)bpm[i - 1]).y;
            }
            Console.WriteLine("Reading pattern...");
            pts.readpattern(pat);
            Console.WriteLine("Generating temp folder");
            string tmpf;
            long tohex(string s)
            {
                long ha1 = 0, ha2 = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    ha1 = ha1 * 277 + s[i];
                    ha1 %= 1000000007;
                    ha2 = ha2 * 1003 + s[i];
                    ha2 %= 1000000009;
                }
                return ha1 * 1000000009 + ha2;
            }
            string HexNum = "0123456789ABCDEF";
            string tofile(string s)
            {
                long x = tohex(s);
                string ans = "";
                while (x > 0)
                {
                    ans += HexNum[Convert.ToInt32(x % 16)];
                    x /= 16;
                }
                ans = "C:\\Users\\Public\\~$MidiCounterGen" + ans;
                return ans;
            }
            tmpf = tofile(filein) + "\\";
            Directory.CreateDirectory(tmpf);
            int faq = trkcnt;
            int bpmptr = 0;
            bool[] died = new bool[trkcnt];
            int[] prec = new int[trkcnt];
            long[] tms = new long[trkcnt];
            double tmm = 0;
            long notecnt = 0;
            int tmdf = 0;
            for (int i = 0; i < delay; i++)
            {
                pts.draw(0, noteall, 120, 0, 0, 0, 0, alltic, tmdf, tmpf);
                Console.WriteLine("Generated frame {0}, 0 notes.", tmdf);
                tmdf++;
            }
            long tmnow = 0;
            long[] history = new long[fps];
            long poly = 0;
            int now = 0;
            while (now < nts.Count)
            {
                for (; now <= tmm; now++)
                {
                    if (now >= nts.Count)
                    {
                        break;
                    }
                    notecnt += Convert.ToInt64(nts[now]);
                    poly += Convert.ToInt64(nts[now]);
                    poly -= Convert.ToInt64(nto[now]);
                }
                pts.draw(notecnt, noteall, 60000000.0 / resol / ((pairli)bpm[bpmptr]).y, 1.0 * (tmdf - delay) / fps, notecnt - history[tmdf % fps], poly, tmm > alltic ? alltic : Convert.ToInt64(tmm), alltic, tmdf, tmpf);
                history[tmdf % fps] = notecnt;
                tmdf++;
                tmnow = Convert.ToInt64((tmdf - delay) * 1000000.0 / fps);
                //Console.WriteLine("{0} {1}", tmc[bpmptr + 1], tmnow);
                while (bpmptr < bpm.Count - 1 && tmc[bpmptr + 1] < Convert.ToDouble(tmnow))
                {
                    bpmptr++;
                }
                tmm = Convert.ToInt32(((pairli)bpm[bpmptr]).x + (tmnow - tmc[bpmptr]) / ((pairli)bpm[bpmptr]).y);
                Console.WriteLine("Generated frame {0}, {1} notes.", tmdf - 1, notecnt);
            }
            for (int i = 0; i < 5 * fps; i++)
            {
                pts.draw(noteall, noteall, 60000000.0 / resol / ((pairli)bpm[bpmptr]).y, 1.0 * (tmdf - delay) / fps, notecnt - history[tmdf % fps], 0, alltic, alltic, tmdf, tmpf);
                history[tmdf % fps] = noteall;
                tmdf++;
                Console.WriteLine("Generated frame {0}, {1} notes.", tmdf - 1, noteall);
            }
            Console.WriteLine("Generating video ...");
            startNewFF(tmpf + "%d.png", fileout);
            Console.WriteLine("Cleaning...");
            Directory.Delete(tmpf, true);
            Console.WriteLine("Converting finished. Press any key to exit...");
            Console.ReadKey();
        }
        public void Render()
        {
            if (isfast == 1)
            {
                FastRender();
            }
            else
            {
                OriginalRender();
            }
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = ("Midi Counter Generator Version 3.0.0.0 by Conjac Jelly Charlieyan");
            mainpart bsp = new mainpart();
            bsp.read();
            bsp.Render();
        }
    }
}
