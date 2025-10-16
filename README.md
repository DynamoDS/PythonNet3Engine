# DSPythonNet3

This project builds the DSPythonNet3 dynamo python engine package.

Building the project will output files in `package_output` that can be used to ship as a Dynamo package.

Builds are published to [Jenkins](https://master-5.jenkins.autodesk.com/job/Dynamo/job/DSPythonNet3/job/master/) for each commit to `master`.

## Deployment

The repository uses the release branch to deploy to [Dynamo Package Manager](www.dynamopackages.com).
As well as to public Nuget: https://www.nuget.org/packages/DynamoVisualProgramming.PythonEngine.PythonNet3

The package `PythonNet3 Engine` is a dynamo package that is consumed by dynamo users using the Package Manager.  The following explains the deployment process to continously push updates to that package.

The component is published publically to `dynamopackages.com` but is published internally for testing purposes to `dev.dynamopackages.com` by default, to publish a new package or version publically set the `use_dev_pm` flag to `false`, and follow these steps:
- Make sure that the latest changes are reviewed and merged to the `master` branch.
- Create a new release branch based on the master branch.The new release branch needs to be named using the following convention:
  - Start the branch name with `release/`
  - If it is a major release, follow it by the major numeric version and the keyword `-major`. Example: release/2-major
  - If it is a minor release, follow it by the major versions and a variable minor version denoted by `x` followed by the keyword `-minor`. Example: release/2.x-minor
  - If it is a patch release, follow it by the major, minor numeric versions and a variable patch version denoted by `x` followed by the keyword `-patch`. Example: release/2.1.x-patch
  - In [Jenkins](https://master-5.jenkins.autodesk.com/job/Dynamo/job/DSPythonNet3/), select the branch and click Build now or Build with Parameters to trigger a new build.
  - After a successful build, a new dynamo package/version will be published to the public dynamo repository, if the flag `use_dev_pm` flag was set to `false`.
  - A PR will be created by the build service user, post build, to merge the new version from the release branch to master. Review and merge to ensure that master is always up-to-date with the latest released version.
  - If for some reason an override of the version is needed, use the flag `package_version` to specify a version that will override the pipeline version.
  
  *Note*: 
    - *The version of the package depends upon the version specified in the `pipeline.yml` file, the version in the branch name is purely for record-keeping*
    - *The current version of the package that is being deployed should always be semantically greater than the already deployed prior version, i.e package version 2.5.0 cannot be published after 3.0.0 is already published.*

**Release Types**
It is **mandatory** to include at-least one of the release types in the branch name.
- Use **patch** for a new patch release (backwards-compatible bug fixes, security patches).
- Use **minor** for a new minor release (add functionality in a backwards-compatible manner).
- Use **major** for a new major release (incompatible API changes/breaking changes).

Examples:
| Branch Name           | Current Version | 	New Version |
| --------------------- | ------------- | ------------- |
| release/4.6.x-patch	  | 4.6.1         |	4.6.2         |
| release/4.x-minor     |	4.6.2         |	4.7.0         |
| release/5-major       |	4.7.0         |	5.0.0         |

[Learn more about semantic versioning](semver.org)

#### Example

```
  -
    type: artifacts
    publish_to_jenkins: true
    publish_to_artifactory: false
    publish_to_package_manager: true
    package_version: 5.6.7 (optional)
    use_dev_pm: false (set to false, to publish to prod)
    outputs:
      - DSPythonNet3.zip
```