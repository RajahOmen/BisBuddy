using BisBuddy.Resources;
using System;
using System.ComponentModel.DataAnnotations;

namespace BisBuddy.Services.Configuration
{
    /// <summary>
    /// Describes the behavior for when the code expects to be on the main
    /// framework thread, and isn't
    /// </summary>
    public enum FrameworkThreadBehaviorType
    {
        /// <summary>
        /// DEFAULT: Log a warning with the stack trace and continues
        /// </summary>
        [Display(
            ResourceType = typeof(Resource),
            Name = nameof(Resource.DebugFrameworkThreadBehaviorWarningName),
            Description = nameof(Resource.DebugFrameworkThreadBehaviorWarningTooltip)
        )]
        Warning,

        /// <summary>
        /// Throw a <see cref="InvalidOperationException"/> exception
        /// </summary>
        [Display(
            ResourceType = typeof(Resource),
            Name = nameof(Resource.DebugFrameworkThreadBehaviorAssertName),
            Description = nameof(Resource.DebugFrameworkThreadBehaviorAssertTooltip)
        )]
        Assert
    }
}
