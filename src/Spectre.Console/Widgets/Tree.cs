namespace Spectre.Console;

/// <summary>
/// Representation of non-circular tree data.
/// Each node added to the tree may only be present in it a single time, in order to facilitate cycle detection.
/// </summary>
public sealed class Tree : Renderable, IHasTreeNodes
{
    private readonly TreeNode _root;

    /// <summary>
    /// Gets Width of the tree.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Gets or sets the tree style.
    /// </summary>
    public Style? Style { get; set; }

    /// <summary>
    ///  Gets or sets the tree guidelines.
    /// </summary>
    public TreeGuide Guide { get; set; } = TreeGuide.Line;

    /// <summary>
    /// Gets the tree's child nodes.
    /// </summary>
    public List<TreeNode> Nodes => _root.Nodes;

    /// <summary>
    /// Gets or sets a value indicating whether or not the tree is expanded or not.
    /// </summary>
    public bool Expanded { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="Tree"/> class.
    /// </summary>
    /// <param name="renderable">The tree label.</param>
    public Tree(IRenderable renderable)
    {
        _root = new TreeNode(renderable);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tree"/> class.
    /// </summary>
    /// <param name="label">The tree label.</param>
    public Tree(string label)
    {
        _root = new TreeNode(new Markup(label));
    }

    /// <inheritdoc />
    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var result = new List<Segment>();
        var visitedNodes = new HashSet<TreeNode>();

        var stack = new Stack<Queue<TreeNode>>();
        stack.Push(new Queue<TreeNode>(new[] { _root }));

        var levels = new List<Segment>();
        levels.Add(GetGuide(options, TreeGuidePart.Continue));

        while (stack.Count > 0)
        {
            var stackNode = stack.Pop();
            if (stackNode.Count == 0)
            {
                levels.RemoveLast();
                if (levels.Count > 0)
                {
                    levels.AddOrReplaceLast(GetGuide(options, TreeGuidePart.Fork));
                }

                continue;
            }

            var isLastChild = stackNode.Count == 1;
            var current = stackNode.Dequeue();
            if (!visitedNodes.Add(current))
            {
                throw new CircularTreeException("Cycle detected in tree - unable to render.");
            }

            stack.Push(stackNode);

            if (isLastChild)
            {
                levels.AddOrReplaceLast(GetGuide(options, TreeGuidePart.End));
            }

            var prefix = levels.Skip(1).ToList();
            var renderableLines = Segment.SplitLines(current.Renderable.Render(options, maxWidth - Segment.CellCount(prefix)));

            var maxLineLength = 0;
            foreach (var (_, isFirstLine, _, line) in renderableLines.Enumerate())
            {
                var prefixLength = 0;
                if (prefix.Count > 0)
                {
                    prefixLength = prefix.Sum(x => x.Text.Length);
                    result.AddRange(prefix.ToList());
                }

                var currentLineLength = line.Sum(x => x.Text.Length) + prefixLength;
                maxLineLength = Math.Max(maxLineLength, currentLineLength);
                result.AddRange(line);
                result.Add(Segment.LineBreak);

                if (isFirstLine && prefix.Count > 0)
                {
                    var part = isLastChild ? TreeGuidePart.Space : TreeGuidePart.Continue;
                    prefix.AddOrReplaceLast(GetGuide(options, part));
                }
            }

            if (current is { Expanded: true, Nodes.Count: > 0 })
            {
                levels.AddOrReplaceLast(GetGuide(options, isLastChild ? TreeGuidePart.Space : TreeGuidePart.Continue));
                levels.Add(GetGuide(options, current.Nodes.Count == 1 ? TreeGuidePart.End : TreeGuidePart.Fork));

                stack.Push(new Queue<TreeNode>(current.Nodes));
            }

            Width = Math.Max(Width, maxLineLength);
        }

        return result;
    }

    /// <summary>
    /// Measures the tree.
    /// </summary>
    /// <param name="options">option.</param>
    /// <param name="maxWidth">Max Width set.</param>
    /// <returns>Measure.</returns>
    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        var width = Math.Min(Width, maxWidth);
        return new Measurement(width, width);
    }

    private Segment GetGuide(RenderOptions options, TreeGuidePart part)
    {
        var guide = Guide.GetSafeTreeGuide(safe: !options.Unicode);
        return new Segment(guide.GetPart(part), Style ?? Style.Plain);
    }
}