using Amazon.CDK.AWS.EC2;

namespace CdkDemo;

public record Network
{
    public IVpc Vpc { get; set; }

    public ISubnetSelection PublicSubnets { get; set; }
    public ISubnetSelection IsolatedSubnets { get; set; }
    public ISubnetSelection PrivateSubnets { get; set; }


    public string Environment = "dev";
    public string Tenant = "au";

    public string GenerateResourceName(string resourceName)
    {
        // ideally utilise variables like environment & tenant to generate resource names
        // i.e. prod-au-resource-name, test-us-resource-name, etc
        return $"{Environment}-{Tenant}-{resourceName}"; 
    }
}