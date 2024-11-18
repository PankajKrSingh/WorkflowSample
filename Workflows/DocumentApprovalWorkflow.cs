using Elsa.Email.Activities;
using Elsa.Expressions.Models;
using Elsa.Extensions;
using Elsa.Http;
using Elsa.Scheduling.Activities;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;
using Elsa.Workflows.Runtime.Activities;
using System.Dynamic;
using System.Net;
using Timer = Elsa.Scheduling.Activities.Timer;



namespace WorkflowApp.Web.Workflows
{
    public class DocumentApprovalWorkflow : WorkflowBase
    {
        protected override void Build(IWorkflowBuilder builder)
        {
            var documentVariable = builder.WithVariable<ExpandoObject>();

            builder.Root = new Sequence
            {
                Activities =
                {
                    new HttpEndpoint
                    {
                        Path = new("/v1/documents"),
                        SupportedMethods = new(new[] { HttpMethods.Post }),
                        ParsedContent = new(documentVariable),
                        CanStartWorkflow = true
                    },
                    new WriteLine(context => $"Document received from {documentVariable.Get<dynamic>(context)!.Author.Name}."),
                    new SendEmail
                    {
                        From = new("workflow@acme.com"),
                        To = new(["josh@acme.com"]),
                        Subject = new(context => $"Document received from {documentVariable.Get<dynamic>(context)!.Author.Name}"),
                        Body = new(context =>
                        {
                            var document = documentVariable.Get<dynamic>(context)!;
                            var author = document!.Author;
                            return $"Document from {author.Name} received for review.<br><a href=\"{GenerateSignalUrl(context, "Approve")}\">Approve</a> or <a href=\"{GenerateSignalUrl(context, "Reject")}\">Reject</a>";
                        })
                    },
                    new WriteHttpResponse
                    {
                        StatusCode = new(HttpStatusCode.OK),
                        Content = new("<h1>Request for Approval Sent</h1><p>Your document has been received and will be reviewed shortly.</p>"),
                        ContentType = new("text/html")
                    },
                    new Fork
                    {
                        JoinMode = ForkJoinMode.WaitAny,
                        Branches =
                        {
                            // Approve
                            new Sequence
                            {
                                Activities =
                                {
                                    new Event("Approve"),
                                    new SendEmail
                                    {
                                        From = new("workflow@acme.com"),
                                        To = new(context => [documentVariable.Get<dynamic>(context)!.Author.Email]),
                                        Subject = new(context => $"Document {documentVariable.Get<dynamic>(context)!.Id} Approved!"),
                                        Body = new(context => $"Great job {documentVariable.Get<dynamic>(context)!.Author.Name}, that document is perfect.")
                                    },
                                    new WriteHttpResponse
                                    {
                                        StatusCode = new(HttpStatusCode.OK),
                                        Content = new("Thanks for the hard work!"),
                                        ContentType = new("text/html")
                                    },
                                }
                            },
                            // Reject
                            new Sequence
                            {
                                Activities =
                                {
                                    new Event("Reject"),
                                    new SendEmail
                                    {
                                        From = new("workflow@acme.com"),
                                        To = new(context => [documentVariable.Get<dynamic>(context)!.Author.Email]),
                                        Subject = new(context => $"Document {documentVariable.Get<dynamic>(context)!.Id} Rejected!"),
                                        Body = new(context => $"Nice try {documentVariable.Get<dynamic>(context)!.Author.Name}, but that document needs more work.")
                                    },
                                    new WriteHttpResponse
                                    {
                                        StatusCode = new(HttpStatusCode.OK),
                                        Content = new("Thanks for the hard work!"),
                                        ContentType = new("text/html")
                                    },
                                }
                            },
                            // Remind
                            //new Sequence
                            //{
                            //    Activities =
                            //    {
                            //        new Timer
                            //        {
                            //           Interval = new(TimeSpan.FromSeconds(10)),
                            //           Name = new("Reminder"),
                            //        },
                            //        new SendEmail
                            //        {
                            //            From = new("workflow@acme.com"),
                            //            To = new(["josh@acme.com"]),
                            //            Subject = new(context => $"{documentVariable.Get<dynamic>(context)!.Author.Name} is waiting for your review!"),
                            //            Body = new(context => $"Don't forget to review document {documentVariable.Get<dynamic>(context)!.Id}. <br><a href=\"{GenerateSignalUrl(context, "Approve")}\">Approve</a> or <a href=\"{GenerateSignalUrl(context, "Reject")}\">Reject</a>")
                            //        }
                            //    }
                            //}
                        }
                    }
                }
            };
        }

        private string GenerateSignalUrl(ExpressionExecutionContext context, string signalName)
        {
            return context.GenerateEventTriggerUrl(signalName);
        }
    }
}
