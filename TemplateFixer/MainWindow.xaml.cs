using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TemplateFixer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            SearchResults.Items.Clear();

            var directory = BasePath.Text;
            var searchPattern = SearchPattern.Text;

            Console.WriteLine($"Searching {directory} for {searchPattern}");

            var fileArray = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            var files = fileArray.Where(file => new[] { @"\bin\", @"\obj\" }.All(segment => !file.Contains(segment)));

            foreach (var file in files)
            {
                var fileJson = File.ReadAllText(file);
                var fileJObject = JObject.Parse(fileJson);

                DistinctTemplate fileTemplate = null;

                foreach (var template in SearchResults.Items.Cast<DistinctTemplate>())
                {
                    if (JToken.DeepEquals(template.JObject, fileJObject))
                    {
                        fileTemplate = template;

                        break;
                    }
                }

                if (fileTemplate is null)
                {
                    fileTemplate = new DistinctTemplate
                    {
                        Index = SearchResults.Items.Count,
                        JObject = fileJObject,
                    };

                    SearchResults.Items.Add(fileTemplate);
                }
                
                fileTemplate.Paths.Add(file.Replace(directory, string.Empty).Replace(searchPattern, string.Empty));
            }

            Console.WriteLine($"Found {SearchResults.Items.Count} distinct results in {files.Count()} total results");
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Fix_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selection = SearchResults.SelectedItems.Cast<DistinctTemplate>();

            ResultPaths.Text = string.Join(
                Environment.NewLine,
                selection.Select(template => string.Join(
                    Environment.NewLine,
                    template.Paths)));

            if (selection.Count() == 1)
            {
                JsonCode.Text = selection.Single().JObject.ToString();
            }
            else
            {
                JsonCode.Text = string.Empty;
            }
        }

        private class DistinctTemplate
        {
            public int Index { get; set; }

            public JObject JObject { get; set; }

            public ICollection<string> Paths { get; } = new HashSet<string>();

            public override string ToString() => $"Type {(char)(Index + 65)} ({Paths.Count} found)";
        }
    }
}
