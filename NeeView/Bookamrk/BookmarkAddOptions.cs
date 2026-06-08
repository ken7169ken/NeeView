using System;
using System.Collections.Generic;
using System.Text;

namespace NeeView
{
    public record BookmarkAddOptions
    {
        public bool AllowDuplicate { get; init; }

        public BookmarkOpenPageMode OpenPageMode { get; init; } = BookmarkOpenPageMode.Resume;
    }
}