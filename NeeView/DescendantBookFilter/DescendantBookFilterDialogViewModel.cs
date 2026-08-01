using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeeView
{
    public class DescendantBookFilterDialogViewModel : ObservableObject
    {
        private bool _isIncludeMode = true;

        public DescendantBookFilterDialogViewModel(
            string targetFolderPath)
        {
            TargetFolderPath        = targetFolderPath;
            TargetFolderDisplayPath = CreateDisplayPath(targetFolderPath);
        }

        // 内部処理用
        public string TargetFolderPath { get; }

        // 画面表示用
        public string TargetFolderDisplayPath { get; }

        private static string CreateDisplayPath(string path)
        {
            const string bookmarkScheme = "bookmark:";
            var body = path.StartsWith(bookmarkScheme, StringComparison.OrdinalIgnoreCase) ? path[bookmarkScheme.Length..] : path;
            var parts = body.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            return string.Join(" > ", new[] { "ブックマーク" }.Concat(parts));
        }

        public bool IsIncludeMode
        {
            get => _isIncludeMode;
            set
            {
                if (SetProperty(ref _isIncludeMode, value)) OnPropertyChanged(nameof(IsExcludeMode));
            }
        }

        public bool IsExcludeMode
        {
            get => !IsIncludeMode;
            set
            {
                if (value) IsIncludeMode = false;
            }
        }
    }
}