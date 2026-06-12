using NeeView.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeeView
{
    public class BookmarkFolderHistory
    {
        private const int _historyCapacity = 100;

        private readonly HistoryLimitedCollection<QueryPath> _history = new(_historyCapacity);

        private readonly BookmarkFolderList _folderList;

        public BookmarkFolderHistory(BookmarkFolderList folder)
        {
            _folderList = folder;
            _history.Changed += (s, e) => Changed?.Invoke(s, e);
        }

        public event EventHandler? Changed;

        public void Add(QueryPath item)
        {
            _history.TrimEnd(null);

            if (item != _history.GetCurrent())
            {
                _history.Add(item);
            }
        }

        public bool CanMoveToPrevious() => _history.CanPrevious();

        public async Task MoveToPreviousAsync()
        {
            if (!_history.CanPrevious()) return;

            var item = _history.GetPrevious();
            if (item is null) return;

            await LoadPageAsync(item);
            _history.Move(-1);
        }

        public bool CanMoveToNext() => _history.CanNext();

        public async Task MoveToNextAsync()
        {
            if (!_history.CanNext()) return;

            var item = _history.GetNext();
            if (item is null) return;

            await LoadPageAsync(item);
            _history.Move(+1);
        }

        private async Task LoadPageAsync(QueryPath? item)
        {
            if (item == null) return;

            await _folderList.MoveToHistoryAsync(item);
        }

        internal List<KeyValuePair<int, QueryPath>> GetHistory(int direction, int size)
        {
            return _history.GetHistory(direction, size);
        }
    }
}