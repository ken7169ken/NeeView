using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;

namespace NeeView
{
    public partial class BookmarkAliasFolder : BookmarkFolder
    {
        public BookmarkAliasFolder()
        {
        }

        public BookmarkAliasFolder(string? name, string? aliasTarget, DateTime entryTime) : base(name ?? "", null, entryTime)
        {
            AliasTarget = aliasTarget;
        }

        public string? AliasTarget { get; set; }
    }
}