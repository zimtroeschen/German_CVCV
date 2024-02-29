using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.G2p;
using Serilog;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("German CVCV Phonemizer", "DE CVCV", "Zim", language: "DE")]
    public class GermanCVCVPhonemizer : SyllableBasedPhonemizer {
        /// <summary>
        /// German CVCV phonemizer that's totally broken and doesn't work at all.
        /// If you manage to fix it, I'll give you 20 euro, pinky promise.
        /// Uses the French VCCV phonemizer by Mim as a base (big shoutout to her for helping me).
        /// </summary>
        /// 

        private readonly string[] vowels = "a,@,6,E,i,O,o,U,u,oe,0,Y,y,aU,aI,OI".Split(',');
        private readonly string[] consonants = "-,b,ch,d,dz,f,g,h,j,k,m,n,N,l,p,pf,r,s,ss,S,t,w,x,z,Z".Split(',');
        private readonly Dictionary<string, string> dictionaryReplacements = ("aa=a,ae=E,ah=@,ao=O,aw=aU,ax=@,ay=aI," +
            "b=b,cc=ch,ch=S,d=d,dh=ss," + "ee=e,eh=E,er=6,ex=6," + "f=f,g=g,hh=h,ih=I,iy=i,jh=dZ,k=k,l=l,m=m,n=n,ng=N," +
            "oe=oe,ohh=0,ooh=o,oy=OI," + "p=p,pf=pf,q=-,r=r;,rr=r;,s=ss,sh=S,t=t," + "th=ss,ts=z," + "ue=y,uh=U,uw=u," + "v=w,w=w,x=x,y=j," +
            "yy=Y," + "z=s,zh=Z").Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "cmudict_de.txt";
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;

        protected override IG2p LoadBaseDictionary() {
            var g2ps = new List<IG2p>();

            // Load dictionary from plugin folder.
            string path = Path.Combine(PluginDir, "de_vccv.yaml");
            if (!File.Exists(path)) {
                Directory.CreateDirectory(PluginDir);
                File.WriteAllBytes(path, Data.Resources.de_vccv_template);
            }
            g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());

            // Load dictionary from singer folder.
            if (singer != null && singer.Found && singer.Loaded) {
                string file = Path.Combine(singer.Location, "de_vccv.yaml");
                if (File.Exists(file)) {
                    try {
                        g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(file)).Build());
                    } catch (Exception e) {
                        Log.Error(e, $"Failed to load {file}");
                    }
                }
            }

            // Load base g2p.
            g2ps.Add(new GermanG2p());

            return new G2pFallbacks(g2ps.ToArray());
        }

        // protected override string[] GetSymbols(Note note) {
        //     string[] original = base.GetSymbols(note);
        //     if (original == null) {
        //         return null;
        //     }
        //     List<string> modified = new List<string>();
        //     string[] diphthongs = new[] { "aU", "OI", "aI" };
        //     foreach (string s in original) {
        //         if (diphthongs.Contains(s)) {
        //             modified.AddRange(new string[] { s[0].ToString(), s[1] + '^'.ToString() });
        //         } else {
        //             modified.Add(s);
        //         }
        //     }
        //     return modified.ToArray();
        // }

        protected override List<string> ProcessSyllable(Syllable syllable) {
            string prevV = syllable.prevV;
            string[] cc = syllable.cc;
            string v = syllable.v;

            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;

            // --------------------------- STARTING V ------------------------------- //
            if (syllable.IsStartingV) {
                basePhoneme = $"-{v}";

            // --------------------------- STARTING CV ------------------------------- //
            } else if (syllable.IsStartingCVWithOneConsonant) {

                basePhoneme = $"{cc[0]}{v}";
                if(!HasOto(basePhoneme,syllable.tone)) {
                    // TODO
                    TryAddPhoneme(phonemes, syllable.tone, $"-{cc[0]}");
                    basePhoneme = $"{cc[0]}{v}";
                }

             // --------------------------- STARTING CCV ------------------------------- //
            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                // TODO
                basePhoneme = $"{cc.Last()}{v}";

                // CC + CV support
                for (int i = 0; i < cc.Length - 1; i++) {
                    var cci = $"{cc[i]}{cc[i + 1]}_";

                    if (i == 0) {
                        cci = $"- {cc[i]}{cc[i + 1]}_";
                        if (!HasOto(cci,syllable.tone)) {
                            cci = $"{cc[i]}{cc[i + 1]}_";
                        }
                    }

                    if (HasOto(cci, syllable.tone)) {
                        phonemes.Add(cci);
                        if (i + 1 == cc.Length - 1 && HasOto($"_{cc.Last()}{v}", syllable.tone)) {
                            basePhoneme = $"_{cc.Last()}{v}";
                        }
                    } else {
                        cci = $"{cc[i]} {cc[i + 1]}";
                        TryAddPhoneme(phonemes, syllable.tone, cci);
                    }
                }


            }
            
            // --------------------------- IS VCV ------------------------------- //
                else if (syllable.IsVCVWithOneConsonant) {

                // try VCV
                var vc = $"{prevV}{cc[0]}";
                phonemes.Add(vc);
                basePhoneme = $"{cc[0]}{v}";

            } else {
                // ------------- IS VCV WITH MORE THAN ONE CONSONANT --------------- //
                var vc = $"{prevV} {cc[0]}";
                phonemes.Add(vc);
                basePhoneme = $"{cc.Last()}{v}";

                // CC + CV support
                for (int i = 0; i < cc.Length - 1; i++) {
                    var cci = $"{cc[i]}{cc[i + 1]}_";

                    if (HasOto(cci, syllable.tone)) {
                        phonemes.Add(cci);
                        if (i + 1 == cc.Length - 1 && HasOto($"_{cc.Last()}{v}", syllable.tone)) {
                            basePhoneme = $"_{cc.Last()}{v}";
                        }
                    } else {
                        cci = $"{cc[i]} {cc[i + 1]}";
                        if (!HasOto(cci, syllable.tone)) {
                            cci = $"{cc[i]}{cc[i + 1]}";
                        }
                        TryAddPhoneme(phonemes, syllable.tone, cci);
                    }

                }

            }

            phonemes.Add(basePhoneme);
            return phonemes;
        }
        protected override List<string> ProcessEnding(Ending ending) {
            string[] cc = ending.cc;
            string v = ending.prevV;

            var phonemes = new List<string>();

            // --------------------------- ENDING V ------------------------------- //
            if (ending.IsEndingV) {
                var vE = $"{v}-";
                phonemes.Add(vE);

            } else {
                // --------------------------- ENDING VC ------------------------------- //
                if (ending.IsEndingVCWithOneConsonant) {

                    // 'VC' + 'C-'
                    var vc = $"{v}{cc[0]}";
                    phonemes.Add(vc);
                    var cE = $"{cc[0]}-";
                    phonemes.Add(cE);

                } else {

                    // --------------------------- ENDING VCC ------------------------------- //
                    var vc = $"{v}{cc[0]}";
                    phonemes.Add(vc);
                    bool hasEnding = false;

                    for (int i = 0; i < cc.Length - 1; i++) {
                        var cci = $"{cc[i]}{cc[i + 1]}";

                        if(i == cc.Length - 2) {
                            cci = $"{cc[i]}{cc[i + 1]} -";
                            hasEnding = true;
                        }
                        if (!HasOto(cci,ending.tone)) {
                            cci = $"{cc[i]}{cc[i + 1]}_";
                            hasEnding = false;
                        }
                        if (!HasOto(cci, ending.tone)) {
                            cci = $"{cc[i]}{cc[i + 1]}";
                            hasEnding = false;
                        }

                        TryAddPhoneme(phonemes, ending.tone, cci);
                    }

                    if (!hasEnding) {
                        var cE = $"{cc.Last()}-";
                        TryAddPhoneme(phonemes, ending.tone, cE);
                    }
                }


            }

            // ---------------------------------------------------------------------------------- //

            return phonemes;
        }

        protected override string ValidateAlias(string alias) {
            foreach (var VV in new[] { "a 6", "a6" }) {
                alias = alias.Replace(VV, "aR");
            }
            // Split diphthongs adjuster
            if (alias.Contains("U^")) {
                alias = alias.Replace("U^", "U");
            }
            if (alias.Contains("I^")) {
                alias = alias.Replace("I^", "I");
            }
            if (alias.Contains("Y^")) {
                alias = alias.Replace("Y^", "Y");
            }
            if (alias.Contains("r;")) {
                alias = alias.Replace("r;", "r");
            }
            if (alias.Contains("6")) {
                alias = alias.Replace("6", "R");
            }
            if (alias.Contains(" ")) {
                alias = alias.Replace(" ", "");
            }
            return alias;
        }

/*         protected override double GetTransitionBasicLengthMs(string alias = "") {
            foreach (var c in longConsonants) {
                if (alias.Contains(c)) {
                    return base.GetTransitionBasicLengthMs() * 2.0;
                }
            }
            return base.GetTransitionBasicLengthMs();
        } */
    }
}