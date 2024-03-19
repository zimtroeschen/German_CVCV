﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenUtau.Api;
using OpenUtau.Core.G2p;
using Serilog;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("German CVCV Phonemizer", "DE CVCV", "Zim, Mim & Cadlaxa", language: "DE")]
    public class GermanCVCVPhonemizer : SyllableBasedPhonemizer {
        /// <summary>
        /// German CVCV phonemizer that's totally broken and doesn't work at all.
        /// If you manage to fix it, I'll give you 20 euro, pinky promise.
        /// Uses the French VCCV phonemizer by Mim as a base (big shoutout to her for helping me).
        /// Cadlaxa here q(≧▽≦q)
        /// </summary>
        /// 

        private readonly string[] vowels = "a,@,6,E,e,i,I,O,o,U,u,oe,0,Y,y,aU,aI,OI".Split(',');
        private readonly string[] consonants = "-,b,ch,d,dZ,f,g,h,j,k,m,n,N,l,p,pf,r,R,s,ss,S,t,w,x,z,Z".Split(',');
        private readonly string[] Normalcons = "b,d,g,k,p,t".Split(',');
        private readonly string[] Affricates = "ch,dZ,x".Split(',');
        private readonly string[] Tap = "r".Split(',');
        private readonly string[] SemiLongcons = "h,y,m,n,N,l,w".Split(',');
        private readonly string[] Longcons = "f,pf,s,ss,S,z,Z,R".Split(',');
        private readonly Dictionary<string, string> dictionaryReplacements = ("aa=a,ae=E,ah=@,ao=O,aw=aU,ax=@,ay=aI," +
            "b=b,cc=ch,ch=S,d=d,dh=ss," + "ee=e,eh=E,er=6,ex=R," + "f=f,g=g,hh=h,ih=I,iy=i,jh=dZ,k=k,l=l,m=m,n=n,ng=N," +
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

        // For banks with missing consonants
        private readonly Dictionary<string, string> missingCphonemes = "w=u,R=6".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingCPhonemes = false;

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
        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (original == null) {
                return null;
            }
            List<string> modified = new List<string>();
            // Splits diphthongs and affricates if not present in the bank
            string[] diphthongs = new[] { "aI", "eI", "OI", "aU", "oU", "ai", "ei", "Oi", "au", "ou", "Ou", "@u", };
            foreach (string s in original) {
                if (diphthongs.Contains(s) && !HasOto($"- {s}", note.tone) && !HasOto(s, note.tone)) {
                    modified.AddRange(new string[] { s[0].ToString(), s[1] + s[1].ToString() });
                } else {
                    modified.Add(s);
                }
            }
            return modified.ToArray();
        }
        private readonly Dictionary<string, string> vvExceptions =
            new Dictionary<string, string>() {
                {"O","w"},
                {"o","w"},
                {"U","w"},
                {"u","w"},
                {"oo","w"},
                {"aU","w"},
                {"Y","i"},
                {"y","i"},
                {"aI","i"},
                {"OI","i"},
                {"i","i"},
                {"6","r"},

            };

        protected override List<string> ProcessSyllable(Syllable syllable) {
            string prevV = syllable.prevV;
            string[] cc = syllable.cc;
            string v = syllable.v;
            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;

            foreach (var entry in missingCphonemes) {
                if (!HasOto("w", syllable.tone) || !HasOto("wR", syllable.tone)) {
                    isMissingCPhonemes = true;
                    break;
                }
            }
            // --------------------------- STARTING V ------------------------------- //
            if (syllable.IsStartingV) {
                var rv = $"-{v}";
                if (HasOto(rv, syllable.vowelTone)) {
                    basePhoneme = rv;
                } else {
                    basePhoneme = v;
                }
                // --------------------------- VV ------------------------------- //
            } else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable) || !AreTonesFromTheSameSubbank(syllable.tone, syllable.vowelTone)) {
                    basePhoneme = $"{prevV}{v}";
                    if (!HasOto(basePhoneme, syllable.vowelTone) && vvExceptions.ContainsKey(prevV) && prevV != v) {
                        // VV IS NOT PRESENT, CHECKS VVEXCEPTIONS LOGIC
                        var vc = $"{prevV}{vvExceptions[prevV]}";
                        if (!HasOto(vc, syllable.vowelTone)) {
                            vc = $"{prevV} {vvExceptions[prevV]}";
                        }
                        TryAddPhoneme(phonemes, syllable.tone, vc);
                        var cv = $"{vvExceptions[prevV]}{v}";
                        basePhoneme = cv;
                    } else {
                        {
                            if (HasOto(v, syllable.vowelTone) || HasOto(ValidateAlias(v), syllable.vowelTone)) {
                                basePhoneme = $"{v}";
                            } else if (!HasOto($"{prevV}{vvExceptions[prevV]}", syllable.vowelTone) || !HasOto(ValidateAlias($"{prevV}{vvExceptions[prevV]}"), syllable.vowelTone)) {
                                // MAKE THEM [V]
                                basePhoneme = $"{v}";
                            } else if (!HasOto(basePhoneme, syllable.vowelTone) || !HasOto(ValidateAlias(basePhoneme), syllable.vowelTone)) {
                                // MAKE THEM A GLOTTAL STOP INSTEAD
                                basePhoneme = $"-{v}";
                                phonemes.Add($"{prevV}-");
                            }
                        }
                    }
                } else {
                    // PREVIOUS ALIAS WILL EXTEND
                    basePhoneme = null;
                }
                // --------------------------- STARTING CV ------------------------------- //
            } else if (syllable.IsStartingCVWithOneConsonant) {
                var rcv = $"- {cc[0]}{v}";
                var cv = $"{cc[0]}{v}";
                if (HasOto(rcv, syllable.vowelTone) || HasOto(ValidateAlias(rcv), syllable.vowelTone)) {
                    basePhoneme = rcv;
                } else if (!HasOto(rcv, syllable.vowelTone) && HasOto(cv, syllable.vowelTone)) {
                    basePhoneme = cv;
                    TryAddPhoneme(phonemes, syllable.tone, $"-{cc[0]}");
                } else {
                    basePhoneme = cv;
                    TryAddPhoneme(phonemes, syllable.tone, $"-{cc[0]}");
                }
                // --------------------------- STARTING CCV ------------------------------- //
            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                var rccv = $"- {string.Join("", cc)}{v}";
                var crv = $"{cc.Last()}{v}";
                var ccv = $"{string.Join("", cc)}{v}";
                if (HasOto(rccv, syllable.vowelTone) || HasOto(ValidateAlias(rccv), syllable.vowelTone)) {
                    basePhoneme = rccv;
                    lastC = 0;
                } else {
                    if (HasOto(ccv, syllable.vowelTone) || HasOto(ValidateAlias(ccv), syllable.vowelTone)) {
                        basePhoneme = ccv;
                    } else if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                        basePhoneme = crv;
                    } else {
                        basePhoneme = $"{cc.Last()}{v}";
                    }
                    // TRY RCC [- CC]
                    for (var i = cc.Length; i > 1; i--) {
                        if (TryAddPhoneme(phonemes, syllable.tone, $"- {string.Join("", cc.Take(i))}", ValidateAlias($"- {string.Join("", cc.Take(i))}"))) {
                            firstC = i - 1;
                        }
                        break;
                    }
                    // [- C]
                    if (phonemes.Count == 0) {
                        TryAddPhoneme(phonemes, syllable.tone, $"-{cc[0]}", ValidateAlias($"-{cc[0]}"));
                    }
                }
            } else {
                var crv = $"{cc.Last()} {v}";
                if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                    basePhoneme = crv;
                } else {
                    basePhoneme = $"{cc.Last()}{v}";
                }
                // try [VC], [V-][-C]
                for (var i = lastC + 1; i >= 0; i--) {
                    var vr = $"{prevV}-";
                    var vc = $"{prevV}{cc[0]}";
                    var vc1 = $"{prevV} {cc[0]}";
                    if (i == 0 && (HasOto(vr, syllable.tone) || HasOto(ValidateAlias(vr), syllable.tone)) && !HasOto(vc, syllable.tone)) {
                        phonemes.Add(vr);
                        phonemes.Add($"-{cc[0]}");
                        break;
                    } else if (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc), syllable.tone)) {
                        phonemes.Add(vc);
                        break;
                    } else if (HasOto(vc1, syllable.tone) || HasOto(ValidateAlias(vc1), syllable.tone)) {
                        phonemes.Add(vc1);
                        break;
                    } else {
                        continue;
                    }
                }
            }
            for (var i = firstC; i < lastC; i++) {
                var cc1 = $"{string.Join("", cc.Skip(i))}";
                var lcv = $"{cc.Last()}{v}";
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // [C1 C2]
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = $"{cc[i]} {cc[i + 1]}";
                }
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = ValidateAlias(cc1);
                }
                // [C1C2]
                if (!HasOto(cc1, syllable.tone)) {
                    cc1 = $"{cc[i]}{cc[i + 1]}";
                }
                if (HasOto(lcv, syllable.tone)) {
                    basePhoneme = lcv;
                }
                if (i + 1 < lastC) {
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1);
                    }
                    // [C1 C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = $"{cc[i]} {cc[i + 1]}";
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1);
                    }
                    // [C1C2]
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = $"{cc[i]}{cc[i + 1]}";
                    }
                    if (HasOto(lcv, syllable.tone)) {
                        basePhoneme = lcv;
                    }
                    if (HasOto(cc1, syllable.tone) && HasOto(cc1, syllable.tone) && !cc1.Contains($"{string.Join("", cc.Skip(i))}")) {
                        // like [VC1] [C1C2] [C2C3] [C3 ..]
                        phonemes.Add(cc1);
                    } else if (TryAddPhoneme(phonemes, syllable.tone, cc1)) {
                        // like [VC1] [C1 C2] [C2 ..]
                        if (cc1.Contains($"{string.Join(" ", cc.Skip(i + 1))}")) {
                            i++;
                        }
                    } else {
                        // like [VC1] [C1] [C2 ..]
                        TryAddPhoneme(phonemes, syllable.tone, cc[i], ValidateAlias(cc[i]));
                    }
                } else {
                    TryAddPhoneme(phonemes, syllable.tone, cc1);
                }
            }

            phonemes.Add(basePhoneme);
            return phonemes;
        }
        protected override List<string> ProcessEnding(Ending ending) {
            string prevV = ending.prevV;
            string[] cc = ending.cc;
            string v = ending.prevV;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;

            // --------------------------- ENDING V ------------------------------- //
            if (ending.IsEndingV) {
                var vE = $"{v}-";
                if (HasOto(vE, ending.tone) || HasOto(ValidateAlias(vE), ending.tone)) {
                    phonemes.Add(vE);
                }

                // --------------------------- ENDING VC ------------------------------- //
            } else if (ending.IsEndingVCWithOneConsonant) {
                // 'VC' + 'C-'
                var vc = $"{v}{cc[0]}";
                var cE = $"{cc[0]}-";
                if (HasOto(vc, ending.tone)) {
                    phonemes.Add(vc);
                    phonemes.Add(cE);
                }
            } else {
                for (var i = lastC; i >= 0; i--) {
                    var vE = $"{v}-";
                    var vcc = $"{v} {cc[0]}";
                    if (i == 0) {
                        if (HasOto(vE, ending.tone)) {
                            phonemes.Add(vE);
                        }
                        break;
                    } else if ((HasOto(vcc, ending.tone) && lastC == 1)) {
                        phonemes.Add(vcc);
                        firstC = 1;
                        if (vcc.EndsWith(cc.Last()) && lastC == 1) {
                            if (consonants.Contains(cc.Last())) {
                                TryAddPhoneme(phonemes, ending.tone, $"{cc.Last()}-", cc.Last());
                            } else {
                                phonemes.Add($"{cc[0]}-");
                            }
                        }
                        firstC = 1;
                        break;
                    } else {
                        phonemes.Add(vcc);
                        break;
                    }
                }
                for (var i = firstC; i < lastC; i++) {
                    var cc1 = $"{cc[i]}{cc[i + 1]}";
                    if (i < cc.Length - 2) {
                        var cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (!HasOto(cc2, ending.tone)) {
                            cc2 = ValidateAlias(cc2);
                        }
                        if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]}{cc[i + 2]}-", ValidateAlias($"{cc[i]} {cc[i + 1]}{cc[i + 2]}-"))) {
                            // like [C1 C2-][C3 ...]
                            i++;
                        } else if (HasOto(cc1, ending.tone) && (HasOto(cc2, ending.tone) || HasOto($"{cc[i + 1]} {cc[i + 2]}-", ending.tone) || HasOto(ValidateAlias($"{cc[i + 1]} {cc[i + 2]}-"), ending.tone))) {
                            // like [C1 C2][C2 ...]
                            phonemes.Add(cc1);
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            i++;
                        } else {
                            // like [C1][C2 ...]
                            TryAddPhoneme(phonemes, ending.tone, cc[i], ValidateAlias(cc[i]), $"{cc[i]} -", ValidateAlias($"{cc[i]} -"));
                            TryAddPhoneme(phonemes, ending.tone, cc[i + 1], ValidateAlias(cc[i + 1]), $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"));
                            i++;
                        }
                    } else {
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = $"{cc[i]} {cc[i + 1]}";
                        }
                        if (!HasOto(cc1, ending.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (TryAddPhoneme(phonemes, ending.tone, $"{cc[i]} {cc[i + 1]}-", ValidateAlias($"{cc[i]} {cc[i + 1]}-"))) {
                            // like [C1 C2-]
                            i++;
                        } else if (TryAddPhoneme(phonemes, ending.tone, cc1, ValidateAlias(cc1))) {
                            // like [C1 C2][C2 -]
                            TryAddPhoneme(phonemes, ending.tone, $"{cc[i + 1]} -", ValidateAlias($"{cc[i + 1]} -"), cc[i + 1], ValidateAlias(cc[i + 1]));
                            i++;
                        } else if (!HasOto(cc1, ending.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", ending.tone)) {
                            // [C1 -] [- C2]
                            TryAddPhoneme(phonemes, ending.tone, $"- {cc[i + 1]}", ValidateAlias($"- {cc[i + 1]}"), cc[i + 1], ValidateAlias(cc[i + 1]));
                            phonemes.Add($"{cc[i]} -");
                            i++;
                        }
                    }
                }
            }
            return phonemes;
        }

        protected override string ValidateAlias(string alias) {
            // VALIDATE ALIAS DEPENDING ON METHOD
            if (isMissingCPhonemes ) {
                foreach (var syllable in missingCphonemes) {
                    alias = alias.Replace(syllable.Key, syllable.Value);
                }
            }
            //VC's
            foreach (var v1 in new[] { "aI", "OI" }) {
                foreach (var c1 in consonants) {
                    if (!alias.Contains($"{v1}{c1}")) {
                        alias = alias.Replace(v1 + c1, "e" + c1);
                    }
                }
            }
            foreach (var v1 in new[] { "aU", "u" }) {
                foreach (var c1 in consonants) {
                    if (!alias.Contains($"{v1}{c1}")) {
                        alias = alias.Replace(v1 + c1, "u" + c1);
                    }
                }
            }
            if (alias == "w-") {
                return alias.Replace("w", "u");
            }
            if (alias == "y-") {
                return alias.Replace("y", "i");
            }
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

        protected override double GetTransitionBasicLengthMs(string alias = "") {
            double transitionMultiplier = 1.0; // Default multiplier
            bool isEndingConsonant = false;
            bool isEndingVowel = false;
            foreach (var c in Normalcons) {
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c}-")) {
                    return base.GetTransitionBasicLengthMs() * 1.3;
                }
            }
            foreach (var c in Tap) {
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c}-")) {
                    return base.GetTransitionBasicLengthMs() * 0.7;
                }
            }
            foreach (var c in Longcons) {
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c}-")) {
                    return base.GetTransitionBasicLengthMs() * 2.3;
                }
            }
            foreach (var c in Affricates) {
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c}-")) {
                    return base.GetTransitionBasicLengthMs() * 1.5;
                }
            }
            foreach (var c in SemiLongcons) {
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c}-")) {
                    return base.GetTransitionBasicLengthMs() * 1.7;
                }
            }
            foreach (var v in vowels) {
                if (alias.Contains(v) && alias.Contains('-') && alias.StartsWith(v)) {
                    isEndingVowel = true;
                    break;
                }
            }

            foreach (var c in consonants) {
                if (alias.Contains(c) && alias.Contains('-')) {
                    isEndingConsonant = true;
                    break;
                }
            }

            // If the alias ends with a consonant, return 0.5 ms
            if (isEndingConsonant) {
                return base.GetTransitionBasicLengthMs() * 0.5;
            }
            // If the alias ends with a vowel, return 1.0 ms
            if (isEndingVowel) {
                return base.GetTransitionBasicLengthMs() * 1.0;
            }
            return base.GetTransitionBasicLengthMs() * transitionMultiplier;
        }
    }
}
