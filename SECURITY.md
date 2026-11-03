# Security policy

The registry is data (circuit files, schemas and the generated index) plus the tools that validate and build it.

If you find a security problem — for example, a way to make the tools run unexpected code or write outside the
repository, or a weakness in how the index is built or published — please **do not open a public issue**. Report it
privately through GitHub's [private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
(the **Security** tab of this repository, «Report a vulnerability»).

Please include what you found, how to reproduce it and the commit you tested. We will acknowledge the report, keep you
informed and credit you in the fix if you wish.

Wrong or doubtful circuit data (a misplaced gate, a wrong name) is not a security problem: open a normal issue or pull
request for it.
