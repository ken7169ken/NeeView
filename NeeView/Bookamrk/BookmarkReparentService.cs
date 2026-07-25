using NeeView.Collections.Generic;
using System;
using System.Linq;

namespace NeeView
{
    internal sealed class BookmarkReparentService
    {
        private readonly Func<TreeListNode<IBookmarkEntry>>                                     _getItems    ;
        private readonly Func<QueryPath, TreeListNode<IBookmarkEntry>?>                         _findNode    ;
        private readonly Func<TreeListNode<IBookmarkEntry>, TreeListNode<IBookmarkEntry>, bool> _merge       ;
        private readonly Func<TreeListNode<IBookmarkEntry>, bool>                               _remove      ;
        private readonly BookmarkTreeRules                                                      _treeRules   ;
        private readonly Action<BookmarkCollectionChangedEventArgs>                             _raiseChanged;

        private enum MoveResult
        { Continue, Completed, Failed, }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public BookmarkReparentService (Func<TreeListNode<IBookmarkEntry>>                                     getItems    ,
                                        Func<QueryPath, TreeListNode<IBookmarkEntry>?>                         findNode    ,
                                        Func<TreeListNode<IBookmarkEntry>, TreeListNode<IBookmarkEntry>, bool> merge       ,
                                        Func<TreeListNode<IBookmarkEntry>, bool>                               remove      ,
                                        BookmarkTreeRules                                                      treeRules   ,
                                        Action<BookmarkCollectionChangedEventArgs>                             raiseChanged)
        {
            _getItems     = getItems     ?? throw new ArgumentNullException(nameof(getItems    ));
            _findNode     = findNode     ?? throw new ArgumentNullException(nameof(findNode    ));
            _merge        = merge        ?? throw new ArgumentNullException(nameof(merge       ));
            _remove       = remove       ?? throw new ArgumentNullException(nameof(remove      ));
            _treeRules    = treeRules    ?? throw new ArgumentNullException(nameof(treeRules   ));
            _raiseChanged = raiseChanged ?? throw new ArgumentNullException(nameof(raiseChanged));
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// 既存ノードを指定した移動先へ移動する
        /// </summary>
        public bool MoveNode (TreeListNode<IBookmarkEntry> item        ,
                              TreeListNode<IBookmarkEntry> target      ,
                              int                          newIndex = 0)
        {
            if (!VerifyMove(item, target, out target)) return false;

            var result = item.Value switch
            {
                BookmarkFolder folder => ResolveFolderMove  (item, target, folder  ),
                Bookmark bookmark     => ResolveBookmarkMove(item, target, bookmark),
                _                     => MoveResult.Failed                          ,
            };

            return result switch
            {
                MoveResult.Completed => true                                 ,
                MoveResult.Failed    => false                                ,
                MoveResult.Continue  => ReparentNode(item, target, newIndex) ,
                _                    => throw new InvalidOperationException(),
            };
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        public bool Merge (TreeListNode<IBookmarkEntry> item, TreeListNode<IBookmarkEntry> target)
        {
            if (item?.  Value is not BookmarkFolder) throw new ArgumentException("item must be BookmarkFolder");
            if (target?.Value is not BookmarkFolder) throw new ArgumentException("target must be BookmarkFolder");

            foreach (var child in item.CloneChildren())
            {
                if (child.Value is BookmarkFolder folder)
                {
                    var conflict = target.WithLock(e => e.Children.FirstOrDefault(e => folder.IsEqual(e.Value)));

                    if (conflict is not null)
                    {
                        Merge(child, conflict);
                        continue;
                    }
                }
                else if (child.Value is Bookmark bookmark)
                {
                    var conflict = target.WithLock(e => e.Children.FirstOrDefault(e => bookmark.IsEqual(e.Value)));

                    if (conflict is not null)
                    {
                        // 重複ノードなので完全削除・破棄
                        _remove(child);
                        continue;
                    }
                }

                ReparentNode(child, target, target.Count);
            }

            return true;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// 移動元と移動先を検証し、
        /// Aliasなら実体ノードへ解決する
        /// </summary>
        private bool VerifyMove (TreeListNode<IBookmarkEntry>     item          ,
                                 TreeListNode<IBookmarkEntry>     target        ,
                                 out TreeListNode<IBookmarkEntry> resolvedTarget)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(target);

            resolvedTarget = target;

            // Moveは既存ノード専用
            if (item.Parent is null) return false;

            // AliasへのD&Dは実体TagへのD&Dとして扱う
            if (resolvedTarget.Value is TagAliasFolder alias)
            {
                var realTarget = _findNode(new QueryPath(alias.AliasTarget));
                if (realTarget is null) return false;
                resolvedTarget = realTarget;
            }
            var items = _getItems();

            // 移動先はルート、またはBookmarkFolderに限る
            if (resolvedTarget != items && resolvedTarget.Value is not BookmarkFolder)
                return false;
            // 自分自身や自分の子孫へは移動できない
            if (item == resolvedTarget) return false;
            if (resolvedTarget.ParentContains(item)) return false;
            // 同じ親へのD&Dは何もしない
            if (item.Parent == resolvedTarget) return false;
            return true;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// フォルダーノードの移動条件を解決する
        /// </summary>
        private MoveResult ResolveFolderMove (TreeListNode<IBookmarkEntry> item  ,
                                              TreeListNode<IBookmarkEntry> target,
                                              BookmarkFolder               folder)
        {
            var items        = _getItems();
            var targetFolder = target.Value as BookmarkFolder;

            var targetKind   = target == items ? TagGroupEntryKind.Edge : targetFolder?.FolderKind;
            var itemKind = GetEntryKind(item.Value);
            var moveKind = GetMoveChildKind(targetKind, itemKind);

            // 空EdgeへEdgeを移動する場合、
            // 移動先を中継フォルダーへ変換する
            if (targetKind == TagGroupEntryKind.Edge && moveKind == TagGroupEntryKind.Edge)
            {
                if (target != items && !IsEmptyEdge(target)) return MoveResult.Failed;
                if (targetFolder is null)                    return MoveResult.Failed;

                targetFolder.FolderKind = null;
                targetKind = null;
            }

            if (!TagGroupFolderKindTools.CanCreateChild(targetKind, moveKind))
            {
                ToastService.Current.Show(new Toast($"{GetFolderKindText(targetKind)}に{GetFolderKindText(moveKind)}を移動することは禁則事項で出来ません。", null, ToastIcon.Warning));
                return MoveResult.Failed;
            }

            // 同名フォルダーがあればMerge
            var conflict = target.WithLock(parent => parent.Children.FirstOrDefault(child => child != item && folder.IsEqual(child.Value)));

            if (conflict is not null) return _merge(item, conflict) ? MoveResult.Completed : MoveResult.Failed;
            if (moveKind != folder.FolderKind) folder.FolderKind = moveKind;

            return MoveResult.Continue;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// Bookmarkノードの移動条件を解決する
        /// </summary>
        private MoveResult ResolveBookmarkMove (TreeListNode<IBookmarkEntry> item  ,
                                                TreeListNode<IBookmarkEntry> target,
                                                Bookmark bookmark                  )
        {
            var items      = _getItems();
            var targetKind = target == items ? TagGroupEntryKind.Edge : (target.Value as BookmarkFolder)?.FolderKind;

            if (!TagGroupFolderKindTools.CanCreateChild(targetKind, TagGroupEntryKind.Bookmark))
            {
                ToastService.Current.Show(new Toast("ブックマークはタグまたは分類フォルダーにのみ移動できます。", null, ToastIcon.Warning));
                return MoveResult.Failed;
            }

            // 同一Bookmarkがあれば移動元を削除
            var conflict = target.WithLock(parent => parent.Children.FirstOrDefault(child => child != item && bookmark.IsEqual(child.Value)));

            if (conflict is not null) return _remove(item) ? MoveResult.Completed : MoveResult.Failed;
            return MoveResult.Continue;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        /// <summary>
        /// 検証済みノードを実際に付け替え、
        /// Moveイベントを通知する
        /// </summary>
        private bool ReparentNode(TreeListNode<IBookmarkEntry> item, TreeListNode<IBookmarkEntry> newParent, int newIndex)
        {
            var oldParent = item.Parent;
            if (oldParent is null) return false;

            var oldIndex = item.GetIndex();
            if (!oldParent.Remove(item)) return false;

            newIndex = Math.Clamp(newIndex, 0, newParent.Count);
            newParent.Insert(newIndex, item);

            _treeRules.PromoteParentToEdgeIfNeeded(newParent, GetEntryKind(item.Value));
            newParent.IsExpanded = true;

            _raiseChanged(new BookmarkCollectionChangedEventArgs(
                              EntryCollectionChangedAction.Move, newParent, item
                              ){ OldParent = oldParent      ,
                                 OldIndex = oldIndex        ,
                                 NewIndex = item.GetIndex() });
            return true;
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        private static TagGroupEntryKind? GetEntryKind(IBookmarkEntry entry)
        {
            return entry switch
            {
                Bookmark              => TagGroupEntryKind.Bookmark,
                TagAliasFolder        => TagGroupEntryKind.Alias   ,
                BookmarkFolder folder => folder.FolderKind         ,
                _                     => null                      ,
            };
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        private static TagGroupEntryKind? GetMoveChildKind(TagGroupEntryKind? parentKind, TagGroupEntryKind? childKind)
        {
            return (parentKind, childKind) switch
            {
                (TagGroupEntryKind.Tag , TagGroupEntryKind.Tag   ) => TagGroupEntryKind.SubTag,
                (null                  , TagGroupEntryKind.SubTag) => TagGroupEntryKind.Tag   ,
                (TagGroupEntryKind.Edge, TagGroupEntryKind.SubTag) => TagGroupEntryKind.Tag   ,
                (TagGroupEntryKind.Edge, null                    ) => TagGroupEntryKind.Edge  ,
                _                                                  => childKind               ,
            };
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        private static string GetFolderKindText(TagGroupEntryKind? kind)
        {
            return kind switch
            {
                null                       => "中継フォルダー"          ,
                TagGroupEntryKind.Tag      => "タグフォルダー"          ,
                TagGroupEntryKind.Category => "分類フォルダー"          ,
                TagGroupEntryKind.Alias    => "エイリアス"              ,
                _                          => kind.ToString() ?? "不明",
            };
        }

        ///===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = ===== = 
        private static bool IsEmptyEdge(TreeListNode<IBookmarkEntry> node)
        {
            return node.Value is BookmarkFolder
            {
                FolderKind: TagGroupEntryKind.Edge
            }
            && node.Children.Count == 0;
        }
    }
}