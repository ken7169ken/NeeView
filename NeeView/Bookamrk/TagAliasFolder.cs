using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace NeeView
{
    public partial class TagAliasFolder : BookmarkFolder
    {
        public TagAliasFolder()
        {
        }

        public TagAliasFolder(string? name, string? aliasTarget, DateTime entryTime) : base(name ?? "", null, entryTime)
        {
            AliasTarget = aliasTarget;
        }

        public string? AliasTarget { get; set; }
    }
}