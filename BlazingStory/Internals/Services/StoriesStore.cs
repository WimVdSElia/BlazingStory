using System.Diagnostics.CodeAnalysis;
using BlazingStory.Internals.Models;

namespace BlazingStory.Internals.Services;

public class StoriesStore
{
    private readonly List<StoryContainer> _StoryContainers = new();

    internal StoriesStore()
    {
    }

    internal IEnumerable<StoryContainer> StoryContainers => this._StoryContainers;

    internal void RegisterStoryContainer(StoryContainer storyContainer)
    {
        var index = this._StoryContainers.FindIndex(container => container.Title == storyContainer.Title);

        if (index != -1)
        {
            if (!ReferenceEquals(this._StoryContainers[index], storyContainer))
            {
                this._StoryContainers[index] = storyContainer;
            }
        }
        else
        {
            this._StoryContainers.Add(storyContainer);
        }
    }

    internal IEnumerable<Story> EnumAllStories()
    {
        return this._StoryContainers.SelectMany(container => container.Stories);
    }

    /// <summary>
    /// Try to find a story by navigation path, such as "examples-ui-button--default".
    /// </summary>
    internal bool TryGetStoryByPath(string navigationPath, [NotNullWhen(true)] out Story? story)
    {
        story = this.EnumAllStories().FirstOrDefault(s => s.NavigationPath == navigationPath);
        return story != null;
    }

    /// <summary>
    /// Try to find a component by navigation path, such as "examples-ui-button".
    /// </summary>
    internal bool TryGetStoryContainerByPath(string navigationPath, [NotNullWhen(true)] out StoryContainer? component)
    {
        component = this._StoryContainers.FirstOrDefault(c => c.NavigationPath == navigationPath);
        return component != null;
    }
}
