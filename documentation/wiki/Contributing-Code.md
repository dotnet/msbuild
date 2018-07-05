### Contributing Code
Before submitting a feature or substantial code contribution please discuss it with the team and ensure it follows the product roadmap. The team rigorously reviews and tests all code submissions. The submissions must meet an extremely high bar for quality, design, backwards compatibility, and roadmap appropriateness.

Because our focus right now is on maintaining backwards compatibility, the team has set the following limits on pull requests:

- Contributions must be discussed with the team first, or they will likely be declined. As our process matures and our experience grows, the team expects to take larger contributions.
- Only contributions referencing an approved Issue will be accepted.
- Pull requests that do not merge easily with the tip of the master branch will be declined. The author will be asked to merge with tip and submit a new pull request.
- Submissions must meet functional and performance expectations, including scenarios for which the team doesn't yet have open source tests. This means you may be asked to fix and resubmit your pull request against a new open test case if it fails one of these tests.
- Submissions must follow the [.Net Foundation Coding Guidelines](https://github.com/dotnet/corefx/wiki/Contributing#c-coding-style)

When you are ready to proceed with making a change, get set up to [[build|Building Testing and Debugging]] the code and familiarize yourself with our workflow and our coding conventions. These two blogs posts on contributing code to open source projects are good too: Open Source Contribution Etiquette by Miguel de Icaza and Don’t “Push” Your Pull Requests by Ilya Grigorik.

You must sign a Contributor License Agreement (CLA) before submitting your pull request. To complete the CLA, submit a request via the form and electronically sign the CLA when you receive the email containing the link to the document. You need to complete the CLA only once to cover all Microsoft Open Technologies OSS projects.

### Developer Workflow

1. Work item is assigned to a developer during the triage process
2. Both Microsoft and external contributors are expected to do their work in a local fork and submit code for consideration via a pull request.
3. When the pull request process deems the change ready it will be merged directly into the tree. 

### Creating New Issues

Please follow these guidelines when creating new issues in the issue tracker:

- Use a descriptive title that identifies the issue to be addressed or the requested feature. For example when describing an issue where the compiler is not behaving as expected, write your bug title in terms of what the product should do rather than what it is doing – “MSBuild should report CS1234 when Xyz is used in Abcd.”
- Do not set any bug fields other than Impact.
- Specify a detailed description of the issue or requested feature.
- For bug reports, please also:
    - Describe the expected behavior and the actual behavior. If it is not self-evident such as in the case of a crash, provide an explanation for why the expected behavior is expected.
    - Provide example code that reproduces the issue.
    - Specify any relevant exception messages and stack traces.
- Subscribe to notifications for the created issue in case there are any follow up questions.

### Coding Conventions
- Use the coding style outlined in the [.Net Foundation Coding Guidelines](https://github.com/dotnet/corefx/wiki/Contributing#c-coding-style)
