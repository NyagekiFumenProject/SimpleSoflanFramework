using Caliburn.Micro;
using OngekiFumenEditor.Core.Utils;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Base
{
    public abstract class OngekiObjectBase : PropertyChangedBase
    {
        private static int ID_GEN = 0;

        public int Id { get; } = ID_GEN++;

        public abstract string IDShortName { get; }

        public string Name => GetType().Name;

        public override string ToString() => $"{{{IDShortName}}} OID[{Id}]";

        public override bool IsNotifying
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => base.IsNotifying;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => base.IsNotifying = value;
        }

        private string tag = string.Empty;

        public string Tag
        {
            get => tag;
            set => Set(ref tag, value);
        }

        public abstract void Copy(OngekiObjectBase fromObj);

        public OngekiObjectBase CopyNew()
        {
            var newObj = CacheLambdaActivator.CreateInstance(GetType()) as OngekiObjectBase;
            newObj.Copy(this);
            return newObj;
        }
    }
}

