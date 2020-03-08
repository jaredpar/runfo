using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

// This file is for data types that appear in the DevOps REST APIs

namespace DevOps.Util
{
    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#build
    /// </summary>
    public sealed class Build
    {
        [JsonProperty("_links")]
        public ReferenceLinks Links { get; set; }
        public AgentSpecification AgentSpecification { get; set; }
        public string BuildNumber { get; set; }        
        public int BuildNumberRevision { get; set; }   
        public BuildController Controller { get; set; }
        public DefinitionReference Definition { get; set; }
        public bool Deleted { get; set; }
        public IdentityRef DeletedBy { get; set; }
        public string DeletedDate { get; set; }
        public string DeletedReason { get; set; }
        public Demand[] Demands { get; set; }    
        public string FinishTime { get; set; }   
        public int Id { get; set; }
        public bool KeepForever { get; set; }
        public IdentityRef LastChangedBy { get; set; }
        public string LastChangedDate { get; set; }   
        public BuildLogReference Logs { get; set; }
        public TaskOrchestrationPlanReference OrchestrationPlan { get; set; }
        public string Parameters { get; set; }
        public TaskOrchestrationPlanReference[] Plans { get; set; }
        public QueuePriority Priority { get; set; }
        public TeamProjectReference Project { get; set; }
        public PropertiesCollection Properties { get; set; }
        public string Quality { get; set; }
        public AgentPoolQueue Queue { get; set; }
        public QueueOptions QueueOptions { get; set; }
        public int QueuePosition { get; set; }
        public string QueueTime { get; set; }
        public BuildReason Reason { get; set; }
        public BuildRepository Repository { get; set; }
        public IdentityRef RequestedBy { get; set; }
        public IdentityRef RequestedFor { get; set; }
        public BuildResult Result { get; set; }
        public bool RetainedByRelease { get; set; }
        public string SourceBranch { get; set; }
        public string SourceVersion { get; set; }
        public string StartTime { get; set; }
        public BuildStatus Status { get; set; }
        public string[] Tags { get; set; }
        public Object TriggerInfo { get; set; }
        public Build TriggeredByBuild { get; set; }
        public string Uri { get; set; }
        public string Url { get; set; }
        public BuildRequestValidationResult[] ValidationResults { get; set; }
        public override string ToString() => $"Id: {Id} BuildNumber: {BuildNumber}";
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-5.1#referencelinks
    /// </summary>
    public sealed class ReferenceLinks
    {
        public object Links { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-5.1#build
    /// </summary>
    public sealed class AgentSpecification
    {
         public string Identifier { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#buildqueryorder
    /// </summary>
    public enum BuildQueryOrder
    {
        FinishTimeAscending,
        FinishTimeDescending,
        QueueTimeAscending,
        QueueTimeDescending,
        StartTimeAscending,
        StartTimeDescending,
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#buildreason
    /// </summary>
    public enum BuildReason
    {
        All,
        BatchedCI,
        BuildCompletion,
        CheckInShelveset,
        IndividualCI,
        Manual,
        None,
        PullRequest,
        Schedule,
        Triggered,
        UserCreated,
        ValidateShelveset
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#buildresult
    /// </summary>
    public enum BuildResult
    {
        Canceled,
        Failed,
        None,
        PartiallySucceeded,
        Succeeded
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#buildstatus
    /// </summary>
    public enum BuildStatus
    {
        All,
        Cancelling,
        Completed,
        InProgress,
        None,
        NotStarted,
        Postponed
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#querydeletedoption
    /// </summary>
    public enum QueryDeletedOption
    {
        excludeDeleted,
        includeDeleted,
        onlyDeleted
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#buildstatus
    /// </summary>
    public sealed class BuildRequestValidationResult
    {
        public string Message { get; set; }
        public ValidationResult Result { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#buildreason
    /// </summary>
    public sealed class BuildRepository
    {
        public bool CheckoutSubModules { get; set; }
        public string Clean { get; set; }
        public string DefaultBranch { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string RootFolder { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#queueoptions
    /// </summary>
    public enum QueueOptions
    {
        // TODO
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#agentpoolqueue
    /// </summary>
    public sealed class AgentPoolQueue
    {
        // TODO
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#propertiescollection
    /// </summary>
    public sealed class PropertiesCollection
    {
        public int Count { get; set; }
        public object Item { get; set; }
        public object[] Keys { get; set; }
        public object[] Values { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#queuepriority
    /// </summary>
    public enum QueuePriority
    {
        AboveNormal,
        BelowNormal,
        High,
        Low,
        Normal
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#buildcontroller
    /// </summary>
    public sealed class BuildController
    {
        // TODO
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#definitionreference
    /// </summary>
    public sealed class DefinitionReference
    {
        public string CreatedDate { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public TeamProjectReference Project { get; set; }
        public DefinitionQueueStatus QueueStatus { get; set; }
        public int Revision { get; set; }
        public DefinitionType Type { get; set; }
        public string Uri { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#definitionqueuestatus
    /// </summary>
    public enum DefinitionQueueStatus
    {
        Disabled,
        Enabled,
        Paused
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#definitiontype
    /// </summary>
    public enum DefinitionType
    {
        Build,
        Xaml
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#identityref
    /// </summary>
    public sealed class IdentityRef
    {
        // This field contains zero or more interesting links about the graph subject. These links may be invoked to obtain additional relationships or more detailed information about this graph subject.
        [JsonProperty("_links")]
        ReferenceLinks Links { get; set; }

        // The descriptor is the primary way to reference the graph subject while the system is running. This field will uniquely identify the same graph subject across both Accounts and Organizations.
        string Descriptor { get; set; }

        // Deprecated - Can be retrieved by querying the Graph user referenced in the "self" entry of the IdentityRef "_links" dictionary
        string DirectoryAlias { get; set; }

        // This is the non-unique display name of the graph subject. To change this field, you must alter its value in the source provider.
        string DisplayName { get; set; }

        string Id { get; set; }

        // Deprecated - Available in the "avatar" entry of the IdentityRef "_links" dictionary
        string ImageUrl { get; set; }

        // Deprecated - Can be retrieved by querying the Graph membership state referenced in the "membershipState" entry of the GraphUser "_links" dictionary
        bool Inactive { get; set; }

        // Deprecated - Can be inferred from the subject type of the descriptor (Descriptor.IsAadUserType/Descriptor.IsAadGroupType)
        bool IsAadIdentity { get; set; }

        // Deprecated - Can be inferred from the subject type of the descriptor (Descriptor.IsGroupType)
        bool IsContainer { get; set; }

        bool IsDeletedInOrigin { get; set; }

        // Deprecated - not in use in most preexisting implementations of ToIdentityRef
        string ProfileUrl { get; set; }

        // Deprecated - use Domain+PrincipalName instead
        string UniqueName { get; set; }

        // This url is the full route to the source resource of this graph subject.
        string Url { get; set; }

    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#taskorchestrationplanreference
    /// </summary>
    public sealed class TaskOrchestrationPlanReference
    {
        public string PlanId { get; set; }
        public int OrchestrationType { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#validationresult
    /// </summary>
    public enum ValidationResult
    {
        Error,
        Ok,
        Warning
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#teamprojectreference
    /// </summary>
    public sealed class TeamProjectReference
    {
        public string Abbreviation { get; set; }
        public string DefaultTeamImageUrl { get; set; }
        public string Description { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public int Revision { get; set; }
        public ProjectState State { get; set; }
        public string Url { get; set; }
        public ProjectVisibility ProjectVisibility { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/core/projects/list?view=azure-devops-rest-5.1#projectstate
    /// </summary>
    public enum ProjectState
    {
        All,
        CreatePending,
        Deleted,
        Deleting,
        New,
        Unchanged,
        WellFormed
    }

    public enum ProjectVisibility
    {
        Private,
        Public
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/list?view=azure-devops-rest-5.0#demand
    /// </summary>
    public sealed class Demand
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/builds/get%20build%20logs?view=azure-devops-rest-5.0#buildlog
    /// </summary>
    public sealed class BuildLog
    {
        public int Id { get; set; }
        public int LineCount { get; set; }
        public string Type { get; set; }
        public string CreatedOn { get; set; }
        public string LastChangedOn { get; set; }
        public string Url { get; set; }

        public override string ToString() => $"Id: {Id} Type: {Type} LineCount: {LineCount}";
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#timeline
    /// </summary>
    public sealed class Timeline
    {
        public string Id { get; set; }
        public int ChangeId { get; set; }
        public string LastChangedBy { get; set; }
        public string LastChangedOn { get; set; }
        public TimelineRecord[] Records { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#timelinerecord
    /// </summary>
    public sealed class TimelineRecord
    { 
        public int Attempt { get; set; }
        public int ChangeId { get; set; }
        public string CurrentOperation { get; set; }
        public TimelineReference Details { get; set; }
        public int ErrorCount { get; set; }
        public string FinishTime { get; set; }
        public string Id { get; set; }
        public Issue[] Issues { get; set; }
        public string LastModified { get; set; }
        public string Name { get; set; }
        public BuildLogReference Log { get; set; }
        public int Order { get; set; }
        /// <summary>
        /// The <see cref="Id"/> field of the timeline record which is this records parent
        /// </summary>
        public string ParentId { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int PercentComplete { get; set; }
        public TimelineAttempt[] PreviousAttempts { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TaskResult Result { get; set; }
        public string ResultCode { get; set; }
        public string StartTime { get; set; }
        public TaskReference Task { get; set; }
        public string Url { get; set; }
        public int WarningCount { get; set; }
        public string WorkerName { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#buildlogreference
    /// </summary>
    public sealed class BuildLogReference
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#issue
    /// </summary>
    public sealed class Issue
    {
        public string Category { get; set; }
        public object Data { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#timelinereference
    /// </summary>
    public sealed class TimelineReference
    {
        public int ChangeId { get; set; }
        public string Id { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#timelineattempt
    /// </summary>
    public sealed class TimelineAttempt
    {
        public int Attempt { get; set; }
        public string RecordId { get; set; }
        public string TimelineId { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#taskresult
    /// </summary>
    public enum TaskResult
    {
        Abandoned,
        Canceled,
        Failed,
        Skipped,
        Succeeded,
        SucceededWithIssues
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#timelinerecordstate
    /// </summary>
    public enum TimelineRecordState
    {
        Completed,
        InProgress,
        Pending
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-5.0#taskreference
    /// </summary>
    public sealed class TaskReference
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/artifacts/get%20artifact?view=azure-devops-rest-5.0#buildartifact
    /// </summary>
    public sealed class BuildArtifact
    {
        [JsonProperty("_links")]
        public object Links { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public ArtifactResource Resource { get; set; }
        public override string ToString() => $"{Name} - {Id}";
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/artifacts/get%20artifact?view=azure-devops-rest-5.0#artifactresource
    /// </summary>
    public sealed class ArtifactResource
    {
        [JsonProperty("_links")]
        public object Links { get; set; }
        public string Data { get; set; }
        public string DownloadUrl { get; set; }
        public object Properties { get; set; }
        public string Type { get; set; }

        /// <summary>
        /// The full http link to the resource
        /// </summary>
        public string Url { get; set; }
    }

    public sealed class BuildDefinition
    {
        [JsonProperty("_links")]
        public object Links { get; set; }
        public IdentityRef AuthoredBy { get; set; }
        public bool BadgeEnabled { get; set; }
        public string BuildNumberFormat { get; set; }
        public string Comment { get; set; }
        public string CreatedDate { get; set; }
        public Demand[] Demands { get; set; }
        public string Description { get; set; }
        public DefinitionReference DraftOf { get; set; }
        public DefinitionReference[] Drafts { get; set; }
        public string DropLocation { get; set; }
        public int Id { get; set; }
        public BuildAuthorizationScope JobAuthorizationScope { get; set; }
        public int JobCancelTimeoutInMinutes { get; set; }
        public int JobTimeoutInMinutes { get; set; }
        public Build LatestBuild { get; set; }
        public Build LatestCompletedBuild { get; set; }
        public BuildMetric[] Metrics { get; set; }
        public string Name { get; set; }
        public BuildOption[] Options { get; set; }
        public string Path { get; set; }
        public BuildProcess Process { get; set; }
        public ProcessParameters ProcessParameters { get; set; }
        public TeamProjectReference Project { get; set; }
        public PropertiesCollection Properties { get; set; }
        public DefinitionQuality Quality { get; set; }
        public AgentPoolQueue Queue { get; set; }
        public DefinitionQueueStatus QueueStatus { get; set; }
        public BuildRepository Repository { get; set; }
        public RetentionPolicy[] RetentionRules { get; set; }
        public int Revision { get; set; }
        public string[] Tags { get; set; }
        public BuildTrigger[] Triggers { get; set; }
        public DefinitionType Type { get; set; }
        public string Uri { get; set; }
        public string Url { get; set; }
        public VariableGroup[] VariableGroups { get; set; }
        public Dictionary<string, BuildDefinitionVariable> Variables { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#buildauthorizationscope
    /// </summary>
    public enum BuildAuthorizationScope
    {
        Project,
        ProjectCollection
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#buildmetric
    /// </summary>
    public sealed class BuildMetric
    {
        public string Date { get; set; }
        public int IntValue { get; set; }
        public string Name { get; set; }
        public string Scope { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#buildoption
    /// </summary>
    public sealed class BuildOption
    {
        public BuildOptionDefinitionReference Definition { get; set; }
        public bool Enabled { get; set; }
        public object Inputs { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#buildoptiondefinitionreference
    /// </summary>
    public sealed class BuildOptionDefinitionReference
    {
        public string Id { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#buildprocess
    /// </summary>
    public sealed class BuildProcess
    {
        public string Type { get; set; }
    }

    public sealed class ProcessParameters
    {
        public DataSourceBindingBase[] DataSourceBindings { get; set; }
        public TaskInputDefinitionBase[] Inputs { get; set; }
        public TaskSourceDefinitionBase[] SourceDefinitions { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#builddefinitionvariable
    /// </summary>
    public sealed class DataSourceBindingBase
    {
        public string CallbackContextTemplate { get; set; }
        public string CallbackRequiredTemplate { get; set; }
        public string DataSourceName { get; set; }
        public string EndpointId { get; set; }
        public string EndpointUrl { get; set; }
        public AuthorizationHeader[] Headers { get; set; }
        public string InitialContextTemplate { get; set; }
        public Object Parameters { get; set; }
        public string RequestContent { get; set; }
        public string RequestVerb { get; set; }
        public string ResultSelector { get; set; }
        public string ResultTemplate { get; set; }
        public string Target { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#authorizationheader
    /// </summary>
    public sealed class AuthorizationHeader
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#taskinputdefinitionbase
    /// </summary>
    public sealed class TaskInputDefinitionBase
    {
        public string[] Aliases { get; set; }
        public string DefaultValue { get; set; }
        public string GroupName { get; set; }
        public string HelpMarkDown { get; set; }
        public string Label { get; set; }
        public string Name { get; set; }
        public Object Options { get; set; }
        public Object Properties { get; set; }
        public bool Required { get; set; }
        public string Type { get; set; }
        public TaskInputValidation Validation { get; set; }
        public string VisibleRule { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#taskinputdefinitionbase
    /// </summary>
    public sealed class TaskSourceDefinitionBase
    {
        public string AuthKey { get; set; }
        public string Endpoint { get; set; }   
        public string KeySelector { get; set; }
        public string Selector { get; set; }
        public string Target { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#taskinputvalidation
    /// </summary>
    public sealed class TaskInputValidation
    {
        public string Expression { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#definitionquality
    /// </summary>
    public enum DefinitionQuality
    {
        Definition,
        Draft
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#retentionpolicy
    /// </summary>
    public sealed class RetentionPolicy
    {
        public string[] ArtifactTypesToDelete { get; set; }
        public string[] Artifacts { get; set; }
        public string[] Branches { get; set; }
        public int DaysToKeep { get; set; }
        public bool DeleteBuildRecord { get; set; }
        public bool DeleteTestResults { get; set; }
        public int MinimumToKeep { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#buildtrigger
    /// </summary>
    public sealed class BuildTrigger
    {
        public DefinitionTriggerType TriggerType { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#definitiontriggertype
    /// </summary>
    public enum DefinitionTriggerType
    {
        None,
        All,
        BatchedContinuousIntegration,
        BatchedGatedCheckIn,
        BuildCompletion,
        ContinuousIntegration,
        GatedCheckIn,
        PullRequest,
        Schedule,
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#variablegroup
    /// </summary>
    public sealed class VariableGroup
    {
        public string Alias { get; set; }
        public string Description { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public Dictionary<string,  BuildDefinitionVariable> Variables { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/build/definitions/get?view=azure-devops-rest-5.1#builddefinitionvariable
    /// </summary>
    public sealed class BuildDefinitionVariable
    {
        public bool AllowOverride { get; set; }
        public bool IsSecret { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/hooks/notifications/create?view=azure-devops-rest-5.1#event
    /// </summary>
    public sealed class Event
    {
        public string CreatedDate { get; set; }
        public FormattedEventMessage DetailedMessage { get; set; }
        public string EventType { get; set; }
        public string Id { get; set; }
        public FormattedEventMessage Message { get; set; }
        public string PublisherId { get; set; }
        public object Resource { get; set; }
        public Dictionary<string, ResourceContainer> ResourceContainers { get; set; }
        public string ResourceVersion { get; set; }
        public SessionToken SessionToken { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/hooks/notifications/create?view=azure-devops-rest-5.1#formattedeventmessage
    /// </summary>
    public sealed class FormattedEventMessage
    {
        public string Html { get; set; }
        public string Markdown { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/hooks/notifications/create?view=azure-devops-rest-5.1#resourcecontainer
    /// </summary>
    public sealed class ResourceContainer
    {
        public string BaseUrl { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// https://docs.microsoft.com/en-us/rest/api/azure/devops/hooks/notifications/create?view=azure-devops-rest-5.1#sessiontoken
    /// </summary>
    public sealed class SessionToken
    {
        public string Error { get; set; }
        public string Token { get; set; }
        public string ValidTo { get; set; }
    }

    public sealed class TestRun
    {
        // Build associated with this test run.
        public ShallowReference Build { get; set; }

        // Build configuration details associated with this test run.
        public BuildConfiguration BuildConfiguration { get; set; }

        // Comments entered by those analyzing the run.
        public string Comment { get; set; }

        // Completed date time of the run.
        public string CompletedDate { get; set; }

        // Test Run Controller.
        public string Controller { get; set; }

        // Test Run CreatedDate.
        public string CreatedDate { get; set; }

        // List of Custom Fields for TestRun.
        public CustomTestField[] CustomFields { get; set; }

        // Drop Location for the test Run.
        public string DropLocation { get; set; }

        // An abstracted reference to some other resource. This class is used to provide the build data contracts with a uniform way to reference other resources in a way that provides easy traversal through links.
        public ShallowReference DtlAutEnvironment { get; set; }

        // An abstracted reference to some other resource. This class is used to provide the build data contracts with a uniform way to reference other resources in a way that provides easy traversal through links.
        public ShallowReference DtlEnvironment { get; set; }

        // This is a temporary class to provide the details for the test run environment.
        public DtlEnvironmentDetails DtlEnvironmentCreationDetails { get; set; }

        // Due date and time for test run.
        public string DueDate { get; set; }

        // Error message associated with the run.
        public string ErrorMessage { get; set; }

        // This class is used to provide the filters used for discovery
        public RunFilter Filter { get; set; }

        // ID of the test run.
        public int Id { get; set; }

        // Number of Incomplete Tests.
        public int IncompleteTests { get; set; }

        // true if test run is automated, false otherwise.
        public bool IsAutomated { get; set; }

        // The iteration to which the run belongs.
        public string Iteration { get; set; }

        // Team foundation ID of the last updated the test run.
        public IdentityRef LastUpdatedBy { get; set; }

        // Last updated date and time
        public string LastUpdatedDate { get; set; }

        // Name of the test run.
        public string Name { get; set; }

        // Number of Not Applicable Tests.
        public int NotApplicableTests { get; set; }

        // Team Foundation ID of the owner of the runs.
        public IdentityRef Owner { get; set; }

        // Number of passed tests in the run
        public int PassedTests { get; set; }

        // Phase/State for the testRun.
        public string Phase { get; set; }

        // Reference of the pipeline to which this test run belongs.
        public PipelineReference PipelineReference { get; set; }

        // Test plan associated with this test run.
        public ShallowReference Plan { get; set; }

        // Post Process State.
        public string PostProcessState { get; set; }

        // Project associated with this run.
        public ShallowReference Project { get; set; }

        // Release Reference for the Test Run.
        public ReleaseReference Release { get; set; }

        // Release Environment Uri for TestRun.
        public string ReleaseEnvironmentUri { get; set; }

        // Release Uri for TestRun.
        public string ReleaseUri { get; set; }

        public int Revision { get; set; }

        // RunSummary by outcome.
        public RunStatistic[] RunStatistics { get; set; }

        // Start date time of the run.
        public string StartedDate { get; set; }

        // The state of the run. Type TestRunState Valid states - Unspecified ,NotStarted, InProgress, Completed, Waiting, Aborted, NeedsInvestigation
        public string State { get; set; }

        // TestRun Substate.
        public TestRunSubstate Substate { get; set; }

        // Tags attached with this test run.
        public TestTag[] Tags { get; set; }

        // Test environment associated with the run.
        public TestEnvironment TestEnvironment { get; set; }

        public int testMessageLogId { get; set; }

        // An abstracted reference to some other resource. This class is used to provide the build data contracts with a uniform way to reference other resources in a way that provides easy traversal through links.
        public ShallowReference TestSettings { get; set; }

        // Total tests in the run
        public int TotalTests { get; set; }

        // Number of failed tests in the run.
        public int UnanalyzedTests { get; set; }

        // Url of the test run
        public string Url { get; set; }

        // Web Access Url for TestRun.
        public string WebAccessUrl { get; set; }

    }

    public sealed class ShallowReference
    {
        // ID of the resource
        public string Id { get; set; }

        // Name of the linked resource (definition name, controller name, etc.)
        public string Name { get; set; }

        // Full http link to the resource
        public string Url { get; set; }

    }

    public sealed class BuildConfiguration
    {
        // Branch name for which build is generated.
        public string BranchName { get; set; }

        // BuildDefinitionId for build.
        public int BuildDefinitionId { get; set; }

        // Build system.
        public string BuildSystem { get; set; }

        // Build Creation Date.
        public string CreationDate { get; set; }

        // Build flavor (eg Build/Release).
        public string Flavor { get; set; }

        // BuildConfiguration Id.
        public int Id { get; set; }

        // Build Number.
        public string Number { get; set; }

        // BuildConfiguration Platform.
        public string Platform { get; set; }

        // Project associated with this BuildConfiguration.
        public ShallowReference Project { get; set; }

        // Repository Guid for the Build.
        public string RepositoryGuid { get; set; }

        // Repository Type (eg. TFSGit).
        public string RepositoryType { get; set; }

        // Source Version(/first commit) for the build was triggered.
        public string SourceVersion { get; set; }

        // Target BranchName.
        public string TargetBranchName { get; set; }

        // Build Uri.
        public string Uri { get; set; }
    }

    public sealed class CustomTestField
    {
        // Field Name.
        public string FieldName { get; set; }

        // Field value.
        public object Value { get; set; }
    }

    public sealed class DtlEnvironmentDetails
    {
        public string CsmContent { get; set; }

        public string CsmParameters { get; set; }

        public string SubscriptionName { get; set; }
    }


    public sealed class RunFilter
    {
        // filter for the test case sources (test containers)
        public string SourceFilter { get; set; }

        // filter for the test cases
        public string TestCaseFilter { get; set; }

    }

    public sealed class PipelineReference
    {
        // Reference of the job
        public JobReference jobReference { get; set; }

        // Reference of the phase.
        public PhaseReference PhaseReference { get; set; }

        // Reference of the pipeline with which this pipeline instance is related.
        public int PipelineId { get; set; }

        // Reference of the stage.
        public StageReference StageReference { get; set; }
    }

    public sealed class PhaseReference
    {
        // Attempt double of the phase
        public int Attempt { get; set; }

        // Name of the phase. Maximum supported length for name is 256 character.
        public string PhaseName { get; set; }

    }

    public sealed class ReleaseReference
    {
        // Number of Release Attempt.
        public int Attempt { get; set; }

        // Release Creation Date.
        public string CreationDate { get; set; }

        // Release definition ID.
        public int DefinitionId { get; set; }

        // Environment creation Date.
        public string EnvironmentCreationDate { get; set; }

        // Release environment definition ID.
        public int EnvironmentDefinitionId { get; set; }

        // Release environment definition name.
        public string EnvironmentDefinitionName { get; set; }

        // Release environment ID.
        public int EnvironmentId { get; set; }

        // Release environment name.
        public string EnvironmentName { get; set; }

        // Release ID.
        public int Id { get; set; }

        // Release name.
        public string Name { get; set; }
    }

    public sealed class RunStatistic
    {
        // Test result count fo the given outcome.
        public int Count { get; set; }

        // Test result outcome
        public string Outcome { get; set; }

        // Test run Resolution State.
        public TestResolutionState ResolutionState { get; set; }

        // State of the test run
        public string State { get; set; }
    }

    public sealed class TestRunSubstate
    {
        // Run state when it is Aborted By the System.
        public string AbortedBySystem { get; set; }

        // Run state after being Analysed.
        public string Analyzed { get; set; }

        // Run state while Creating Environment.
        public string CanceledByUser { get; set; }

        // Run state when cancellation is in Progress.
        public string CancellationInProgress { get; set; }

        // Run state while Creating Environment.
        public string CreatingEnvironment { get; set; }

        // Run with noState.
        public string None { get; set; }

        // Run state while Pending Analysis.
        public string PendingAnalysis { get; set; }

        // Run state while Running Tests.
        public string RunningTests { get; set; }

        // Run state when run has timedOut.
        public string TimedOut { get; set; }
    }

    public sealed class TestTag
    {
        public string Name { get; set; }
    }

    public sealed class TestEnvironment
    {
        // Test Environment Id.
        public string EnvironmentId	{ get; set; }

        // Test Environment Name.
        public string EnvironmentName { get; set; }

    }

    public sealed class JobReference
    {
        // Attempt double of the job
        public int Attempt { get; set; }

        // Matrixing in YAML generates copies of a job with different inputs in matrix. JobName is the name of those input. Maximum supported length for name is 256 character.
        public string JobName { get; set; }
    }

    public sealed class StageReference
    {
        // Attempt double of stage
        public int Attempt { get; set; }

        // Name of the stage. Maximum supported length for name is 256 character
        public string StageName { get; set; }

    }

    public sealed class TestResolutionState
    {
        // Test Resolution state Id.
        public int Id { get; set; }

        // Test Resolution State Name.
        public string Name { get; set; }

        // An abstracted reference to some other resource. This class is used to provide the build data contracts with a uniform way to reference other resources in a way that provides easy traversal through links.
        public ShallowReference Project { get; set; }

    }

    public sealed class TestCaseResult
    {
        // Test attachment ID of action recording.
        public int AfnStripId { get; set; }

        // Reference to area path of test.
        public ShallowReference Area { get; set; }

        // Reference to bugs linked to test result.
        public ShallowReference[] AssociatedBugs { get; set; }

        // ID representing test method in a dll.
        public string AutomatedTestId { get; set; }

        // Fully qualified name of test executed.
        public string AutomatedTestName { get; set; }

        // Container to which test belongs.
        public string AutomatedTestStorage { get; set; }

        // Type of automated test.
        public string AutomatedTestType { get; set; }

        // TypeId of automated test.
        public string AutomatedTestTypeId { get; set; }

        // Shallow reference to build associated with test result.
        public ShallowReference Build { get; set; }

        // Reference to build associated with test result.
        public BuildReference BuildReference { get; set; }

        // Comment in a test result with maxSize= 1000 chars.
        public string Comment { get; set; }

        // Time when test execution completed. Completed date should be greater than StartedDate.
        public string CompletedDate { get; set; }

        // Machine name where test executed.
        public string ComputerName { get; set; }

        // Reference to test configuration. Type ShallowReference.
        public ShallowReference Configuration { get; set; }

        // Timestamp when test result created.
        public string CreatedDate { get; set; }

        // Additional properties of test result.
        public CustomTestField[] CustomFields { get; set; }

        // Duration of test execution in milliseconds. If not provided value will be set as CompletedDate - StartedDate
        public double DurationInMs { get; set; }

        // Error message in test execution.
        public string ErrorMessage { get; set; }

        // Information when test results started failing.
        public FailingSince FailingSince { get; set; }

        // Failure type of test result. Valid Value= (Known Issue, New Issue, Regression, Unknown, None)
        public string FailureType { get; set; }

        // ID of a test result.
        public int Id { get; set; }

        // Test result details of test iterations used only for Manual Testing.
        public TestIterationDetailsModel[] IterationDetails { get; set; }

        // Reference to identity last updated test result.
        public IdentityRef LastUpdatedBy { get; set; }

        // Last updated datetime of test result.
        public string LastUpdatedDate { get; set; }

        // Test outcome of test result. Valid values = (Unspecified, None, Passed, Failed, Inconclusive, Timeout, Aborted, Blocked, NotExecuted, Warning, Error, NotApplicable, Paused, InProgress, NotImpacted)
        public string Outcome { get; set; }

        // Reference to test owner.
        public IdentityRef Owner { get; set; }

        // Priority of test executed.
        public int Priority { get; set; }

        // Reference to team project.
        public ShallowReference Project { get; set; }

        // Shallow reference to release associated with test result.
        public ShallowReference Release { get; set; }

        // Reference to release associated with test result.
        public ReleaseReference ReleaseReference { get; set; }

        // ResetCount.
        public int ResetCount { get; set; }

        // Resolution state of test result.
        public string ResolutionState { get; set; }

        // ID of resolution state.
        public int ResolutionStateId { get; set; }

        // Hierarchy type of the result, default value of None means its leaf node.
        public ResultGroupType ResultGroupType { get; set; }

        // Revision double of test result.
        public int Revision { get; set; }

        // Reference to identity executed the test.
        public IdentityRef RunBy { get; set; }

        // Stacktrace with maxSize= 1000 chars.
        public string StackTrace { get; set; }

        // Time when test execution started.
        public string StartedDate { get; set; }

        // State of test result. Type TestRunState.
        public string State { get; set; }

        // List of sub results inside a test result, if ResultGroupType is not None, it holds corresponding type sub results.
        public TestSubResult[] SubResults { get; set; }

        // Reference to the test executed.
        public ShallowReference TestCase { get; set; }

        // Reference ID of test used by test result. Type TestResultMetaData
        public int TestCaseReferenceId { get; set; }

        // TestCaseRevision Number.
        public int TestCaseRevision { get; set; }

        // Name of test.
        public string TestCaseTitle { get; set; }

        // Reference to test plan test case workitem is part of.
        public ShallowReference TestPlan { get; set; }

        // Reference to the test point executed.
        public ShallowReference TestPoint { get; set; }

        // Reference to test run.
        public ShallowReference TestRun { get; set; }

        // Reference to test suite test case workitem is part of.
        public ShallowReference TestSuite { get; set; }

        // Url of test result.
        public string Url { get; set; }
    }

    public sealed class FailingSince
    {
        // Build reference since failing.
        public BuildReference Build { get; set; }

        // Time since failing.
        public string Date { get; set; }

        // Release reference since failing
        public ReleaseReference Release { get; set; }

    }

    public sealed class BuildReference
    {
        // Branch name.
        public string BranchName { get; set; }

        // Build system.
        public string BuildSystem { get; set; }

        // Build Definition ID.
        public int DefinitionId { get; set; }

        // Build ID.
        public int Id { get; set; }

        // Build Number.
        public string Number { get; set; }

        // Repository ID.
        public string RepositoryId { get; set; }

        // Build URI.
        public string Uri { get; set; }

    }

    public sealed class TestIterationDetailsModel
    {

        // Test step results in an iteration.
        public TestActionResultModel[] ActionResults { get; set; }

        // Reference to attachments in test iteration result.
        public TestCaseResultAttachmentModel[] Attachments { get; set; }

        // Comment in test iteration result.
        public string Comment { get; set; }

        // Time when execution completed.
        public string CompletedDate { get; set; }

        // Duration of execution.
        public double DurationInMs { get; set; }

        // Error message in test iteration result execution.
        public string ErrorMessage { get; set; }

        // ID of test iteration result.
        public int Id { get; set; }

        // Test outcome if test iteration result.
        public string Outcome { get; set; }

        // Test parameters in an iteration.
        public TestResultParameterModel[] Parameters { get; set; }

        // Time when execution started.
        public string StartedDate { get; set; }

        // Url to test iteration result.
        public string Url { get; set; }
    }

    public enum ResultGroupType
    {
        None,
        DataDriven,

        Generic,

        OrderedTest,

        Rerun,
    }

    public sealed class TestSubResult
    {
        // Comment in sub result.
        public string Comment { get; set; }

        // Time when test execution completed.
        public string CompletedDate { get; set; }

        // Machine where test executed.
        public string ComputerName { get; set; }

        // Reference to test configuration.
        public ShallowReference Configuration { get; set; }

        // Additional properties of sub result.
        public CustomTestField[] CustomFields { get; set; }

        // Name of sub result.
        public string DisplayName { get; set; }

        // Duration of test execution.
        public int DurationInMs { get; set; }

        // Error message in sub result.
        public string ErrorMessage { get; set; }

        // ID of sub result.
        public int Id { get; set; }

        // Time when result last updated.
        public string LastUpdatedDate { get; set; }

        // Outcome of sub result.
        public string Outcome { get; set; }

        // Immediate parent ID of sub result.
        public int ParentId { get; set; }

        // Hierarchy type of the result, default value of None means its leaf node.
        public ResultGroupType ResultGroupType { get; set; }

        // Index double of sub result.
        public int SequenceId { get; set; }

        // Stacktrace.
        public string StackTrace { get; set; }

        // Time when test execution started.
        public string StartedDate { get; set; }

        // List of sub results inside a sub result, if ResultGroupType is not None, it holds corresponding type sub results.
        public TestSubResult[] SubResults { get; set; }

        // Reference to test result.
        public TestCaseResultIdentifier TestResult { get; set; }

        // Url of sub result.
        public string Url { get; set; }

    }

    public sealed class TestActionResultModel
    {

        // Path identifier test step in test case workitem.
        public string ActionPath { get; set; }

        // Comment in result.
        public string Comment { get; set; }

        // Time when execution completed.
        public string CompletedDate { get; set; }

        // Duration of execution.
        public double DurationInMs { get; set; }

        // Error message in result.
        public string ErrorMessage { get; set; }

        // Iteration ID of test action result.
        public int IterationId { get; set; }

        // Test outcome of result.
        public string Outcome { get; set; }

        // Reference to shared step workitem.
        public SharedStepModel SharedStepModel { get; set; }

        // Time when execution started.
        public string StartedDate { get; set; }

        // This is step Id of test case. For shared step, it is step Id of shared step in test case workitem; step Id in shared step. Example: TestCase workitem has two steps: 1) Normal step with Id = 1 2) Shared Step with Id = 2. Inside shared step: a) Normal Step with Id = 1 Value for StepIdentifier for First step: "1" Second step: "2;1"
        public string StepIdentifier { get; set; }

        // Url of test action result.
        public string Url { get; set; }
    }

    public sealed class TestCaseResultAttachmentModel
    {

        // Path identifier test step in test case workitem.
        public string ActionPath { get; set; }

        // Attachment ID.
        public int Id { get; set; }

        // Iteration ID.
        public int IterationId { get; set; }

        // Name of attachment.
        public string Name { get; set; }

        // Attachment size.
        public int Size { get; set; }

        // Url to attachment.
        public string Url { get; set; }

    }

    public sealed class TestResultParameterModel
    {
        // Test step path where parameter is referenced.
        public string ActionPath { get; set; }

        // Iteration ID.
        public int IterationId { get; set; }

        // Name of parameter.
        public string ParameterName { get; set; }

        // This is step Id of test case. For shared step, it is step Id of shared step in test case workitem; step Id in shared step. Example: TestCase workitem has two steps: 1) Normal step with Id = 1 2) Shared Step with Id = 2. Inside shared step: a) Normal Step with Id = 1 Value for StepIdentifier for First step: "1" Second step: "2;1"
        public string StepIdentifier { get; set; }

        // Url of test parameter.
        public string Url { get; set; }

        // Value of parameter.
        public string Value { get; set; }

    }

    public sealed class TestCaseResultIdentifier
    {
        // Test result ID.
        public int TestResultId { get; set; }

        // Test run ID.
        public int TestRunId { get; set; }
    }

    public sealed class SharedStepModel
    {
        // WorkItem shared step ID.
        public int Id { get; set; }

        // Shared step workitem revision.
        public int Revision { get; set; }
    }

    public sealed class TestAttachment
    {
        // Attachment type.
        public AttachmentType AttachmentType { get; set; }

        // Comment associated with attachment.
        public string Comment { get; set; }

        // Attachment created date.
        public string CreatedDate { get; set; }

        // Attachment file name
        public string FileName { get; set; }

        // ID of the attachment.
        public int Id { get; set; }

        // Attachment size.
        public int Size { get; set; }

        // Attachment Url.
        public string Url { get; set; }
    }
    
    public enum AttachmentType
    {
        CodeCoverage,
        ConsoleLog,
        GeneralAttachment,
    }
}
