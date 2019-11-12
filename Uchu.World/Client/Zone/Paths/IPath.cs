namespace Uchu.World.Client
{
    public interface IPath
    {
        string Name { get; set; }
        PathType Type { get; set; }
        PathBehavior Behavior { get; set; }
        IPathWaypoint[] Waypoints { get; set; }
    }
}