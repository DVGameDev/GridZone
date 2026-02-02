using NorskaLib.Spreadsheets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NorskaLibExamples.Spreadsheets
{
    [Serializable]
    public class SpreadsheetContent
    {
        [SpreadsheetPage("Units")]
        public List<UnitCfg> Units;
        [SpreadsheetPage("Effects")]
        public List<EffectCfg> Effects;
       
    }
    [CreateAssetMenu(fileName = "SpreadsheetContainer", menuName = "SpreadsheetContainer")]
    public class SpreadsheetContainer : SpreadsheetsContainerBase
    {
        [SpreadsheetContent]
        [SerializeField] SpreadsheetContent content;
        public SpreadsheetContent Content => content;
    }
}

