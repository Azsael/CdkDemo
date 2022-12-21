using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.Route53;

namespace CdkDemo;

internal static class FargatePatternRunner
{
    public static void BuildFargateViaPattern(this Stack stack, Network network)
    {
        var cluster = new Cluster(stack, "pattern-cluster", new ClusterProps
        {
            Vpc = network.Vpc,
            ClusterName = network.GenerateResourceName("pattern-cluster"),
            ContainerInsights = true,
        });

        var hostedZone = new HostedZone(stack, "pattern-host", new HostedZoneProps { ZoneName = "bookwana.com" });

        var certificate = new DnsValidatedCertificate(stack, "cert", new DnsValidatedCertificateProps
        {
            HostedZone = hostedZone,
            DomainName = "test.bookwana.com",
        });

        var dbSecurityGroup = SecurityGroup.FromLookupByName(stack, "db-security", "", network.Vpc);

        var securityGroup = new SecurityGroup(stack, "security-boop", new SecurityGroupProps());
        
        dbSecurityGroup.AddIngressRule(securityGroup, Port.Tcp(3306));
        securityGroup.AddEgressRule(dbSecurityGroup, Port.AllTcp());



        var service = new ApplicationLoadBalancedFargateService(stack, "pattern-service",
            new ApplicationLoadBalancedFargateServiceProps
            {
                Cluster = cluster,
                AssignPublicIp = false,
                PublicLoadBalancer = true,
                CircuitBreaker = new DeploymentCircuitBreaker { Rollback = true },
                TaskSubnets = network.PrivateSubnets,

                TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
                {
                    Image = ContainerImage.FromEcrRepository(Repository.FromRepositoryName(stack, "", "repo-anme"), "VERSION"),
                    Environment = new Dictionary<string, string>
                    {
                        { "ASPNETCORE_ENVIRONMENT", "Production" }, // add enviro variables here
                    },
                    Secrets = null, // add secrets here
                },
                ServiceName = network.GenerateResourceName("pattern-service"),
                SecurityGroups = new []{ securityGroup },
                DomainZone = hostedZone,
                DomainName = "test.bookwana.com",
                Certificate = certificate,
                DesiredCount = 1,
                Cpu = 1024,
                MemoryLimitMiB = 2048, // Default is 256
                RedirectHTTP = true,
                Protocol = ApplicationProtocol.HTTPS,
                HealthCheckGracePeriod = Duration.Seconds(5),
                IdleTimeout = Duration.Seconds(30),
                ProtocolVersion = ApplicationProtocolVersion.HTTP2,
            }
        );

        // service.TaskDefinition.TaskRole
        // use this role to grant access to s3, param store, aws features, etc

        // alarm example
        service.Service.MetricCpuUtilization().CreateAlarm(stack, "", new CreateAlarmOptions
        {
            EvaluationPeriods = 3,
            TreatMissingData = TreatMissingData.IGNORE,
            Threshold = 50,
        });

        var scaling = service.Service.AutoScaleTaskCount(new EnableScalingProps
        {
            MinCapacity = 5, // this overrides above desired
            MaxCapacity = 20,
            
        });
        
        // many options for scaling
        scaling.ScaleOnCpuUtilization("", new CpuUtilizationScalingProps
        {
            TargetUtilizationPercent = 20,
            // many more options
        });
    }
}