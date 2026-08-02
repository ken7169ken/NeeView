using NeeView.Threading;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NeeView
{
    /// <summary>
    /// SearchBox.xaml の相互作用ロジック
    /// </summary>
    public partial class SearchBox : UserControl
    {
        public readonly static RoutedCommand DeleteAction = new RoutedCommand("DeleteAction", typeof(SearchBox));

        private readonly DelayAction _delayAction = new();
        private int _requestSearchBoxFocusValue;
        
        private readonly DelayAction _rootSearchDelayAction = new();
        public event EventHandler? RootSearchExecuted;

        public SearchBox()
        {
            InitializeComponent();
            UpdateRootSearchBoxLayout();

            this.CommandBindings.Add(new CommandBinding(DeleteAction, DeleteAction_Execute));

            this.SearchBoxRoot.DataContext = this;
            this.SearchBoxRoot.IsKeyboardFocusWithinChanged += SearchBoxRoot_IsKeyboardFocusWithinChanged;
        }

        //public bool IsVisibleRootSearchBox
        //{
        //    get { return (bool)GetValue(IsVisibleRootSearchBoxProperty); }
        //    set { SetValue(IsVisibleRootSearchBoxProperty, value); }
        //}

        //======================================================================================================================
        // ここから
        public static readonly DependencyProperty IsVisibleRootSearchBoxProperty = DependencyProperty.Register(
                                                                                       nameof(IsVisibleRootSearchBox),
                                                                                       typeof(bool),
                                                                                       typeof(SearchBox),
                                                                                       new PropertyMetadata(false, IsVisibleRootSearchBoxPropertyChanged)
                                                                                   );
        public bool IsVisibleRootSearchBox
        {
            get => (bool)GetValue(IsVisibleRootSearchBoxProperty);
            set => SetValue(IsVisibleRootSearchBoxProperty, value);
        }

        private static void IsVisibleRootSearchBoxPropertyChanged(DependencyObject d,　DependencyPropertyChangedEventArgs e)
        {
            if (d is SearchBox searchBox)　searchBox.UpdateRootSearchBoxLayout();
        }

        private void UpdateRootSearchBoxLayout()
        {
            if (!IsInitialized)　return;

            if (IsVisibleRootSearchBox)
            {
                RootSearchColumn.Width = new GridLength(12.0, GridUnitType.Star);
                RootSearchSeparatorColumn.Width = new GridLength(6.0);
                MainSearchColumn.Width = new GridLength(88.0, GridUnitType.Star);
            }
            else
            {
                RootSearchColumn.Width = new GridLength(0.0);
                RootSearchSeparatorColumn.Width = new GridLength(0.0);
                MainSearchColumn.Width = new GridLength(1.0, GridUnitType.Star);
            }
        }
        // ここまで。
        //======================================================================================================================

        /// <summary>
        /// 検索エラーメッセージ
        /// </summary>
        public string SearchKeywordErrorMessage
        {
            get { return (string)GetValue(SearchKeywordErrorMessageProperty); }
            set { SetValue(SearchKeywordErrorMessageProperty, value); }
        }

        public static readonly DependencyProperty SearchKeywordErrorMessageProperty =
            DependencyProperty.Register("SearchKeywordErrorMessage", typeof(string), typeof(SearchBox), new PropertyMetadata(""));

        /// <summary>
        /// 検索キーワード
        /// </summary>
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SearchBox), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// 検索キーワード候補。検索履歴とか。
        /// </summary>
        public IEnumerable? ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(SearchBox), new PropertyMetadata(null));

        /// <summary>
        /// 検索コマンド
        /// </summary>
        public ICommand? SearchCommand
        {
            get { return (ICommand)GetValue(SearchCommandProperty); }
            set { SetValue(SearchCommandProperty, value); }
        }

        public ICommand? RootSearchCommand
        {
            get { return (ICommand)GetValue(RootSearchCommandProperty); }
            set { SetValue(RootSearchCommandProperty, value); }
        }

        public static readonly DependencyProperty RootSearchCommandProperty
            = DependencyProperty.Register(nameof(RootSearchCommand), typeof(ICommand), typeof(SearchBox), new PropertyMetadata(null));

        public static readonly DependencyProperty SearchCommandProperty =
            DependencyProperty.Register("SearchCommand", typeof(ICommand), typeof(SearchBox), new PropertyMetadata(null));

        private void RootSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            Text = this.RootSearchTextBox.Text;

            if (RootSearchCommand?.CanExecute(null) == true)
            {
                RootSearchCommand.Execute(null);
                RootSearchExecuted?.Invoke(this, EventArgs.Empty);
            }

            e.Handled = true;
        }

        /// <summary>
        /// ルート検索ボックスのテキスト 遅延検索
        /// </summary>
        private void RootSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            _rootSearchDelayAction.Request(() =>
            {
                Text = textBox.Text;

                if (RootSearchCommand?.CanExecute(null) == true)
                {
                    RootSearchCommand.Execute(null);
                }
            },
            TimeSpan.FromMilliseconds(500));
        }

        /// <summary>
        /// 履歴削除コマンド
        /// </summary>
        public ICommand? DeleteCommand
        {
            get { return (ICommand)GetValue(DeleteCommandProperty); }
            set { SetValue(DeleteCommandProperty, value); }
        }

        public static readonly DependencyProperty DeleteCommandProperty =
            DependencyProperty.Register("DeleteCommand", typeof(ICommand), typeof(SearchBox), new PropertyMetadata(null));


        /// <summary>
        /// キーボードフォーカス変更
        /// </summary>
        private void SearchBoxRoot_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!this.SearchBoxRoot.IsKeyboardFocusWithin)
            {
                Text = this.SearchBoxComboBox.Text;
                Search();
            }
        }

        /// <summary>
        /// クリアボタン
        /// </summary>
        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            Text = "";
        }

        /// <summary>
        /// 単キーのショートカット無効
        /// </summary>
        private void Control_KeyDown_IgnoreSingleKeyGesture(object? sender, KeyEventArgs e)
        {
            KeyExGesture.AddFilter(KeyExGestureFilter.All);
        }

        /// <summary>
        /// キー入力
        /// </summary>
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            KeyExGesture.AddFilter(KeyExGestureFilter.All);

            if (e.Key == Key.Enter)
            {
                Text = this.SearchBoxComboBox.Text;
                Search();
            }
        }

        /// <summary>
        /// テキストボックスのテキスト 遅延反映
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (e.OriginalSource is not TextBox textBox) return;

            _delayAction.Request(() =>
            {
                this.Text = textBox.Text;
            },
            TimeSpan.FromMilliseconds(500));
        }

        /// <summary>
        /// 検索ボックスのフォーカス要求処理
        /// </summary>
        public void FocusAsync()
        {
            if (Interlocked.Exchange(ref _requestSearchBoxFocusValue, 1) == 0)
            {
                _ = FocusSearchBoxAsync(); // 非同期
            }
        }

        /// <summary>
        /// 検索ボックスにフォーカスをあわせる。
        /// </summary>
        /// <returns></returns>
        private async Task FocusSearchBoxAsync()
        {
            // 表示が間に合わない場合があるので繰り返しトライする
            for (int i = 0; i < 10; i++)
            {
                var searchBox = this;
                if (searchBox != null && searchBox.IsLoaded && searchBox.IsVisible && this.IsVisible)
                {
                    searchBox.FocusEditableTextBox();
                    var isFocused = searchBox.IsKeyboardFocusWithin;
                    //Debug.WriteLine($"Focus: {isFocused}");
                    if (isFocused) break;
                }

                //Debug.WriteLine($"Focus: ready...");
                await Task.Delay(100);
            }

            Interlocked.Exchange(ref _requestSearchBoxFocusValue, 0);
            //Debug.WriteLine($"Focus: done.");
        }

        /// <summary>
        /// テキストボックスにフォーカス
        /// </summary>
        private bool FocusEditableTextBox()
        {
            return this.SearchBoxComboBox.Focus();
        }

        /// <summary>
        /// 検索実行
        /// </summary>
        private void Search()
        {
            if (SearchCommand?.CanExecute(null) == true)
            {
                SearchCommand?.Execute(null);
            }
        }

        /// <summary>
        /// 履歴削除実行
        /// </summary>
        private void DeleteAction_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            var content = e.Parameter as string;
            if (string.IsNullOrEmpty(content)) return;

            DeleteCommand?.Execute(content);
        }
    }
}
