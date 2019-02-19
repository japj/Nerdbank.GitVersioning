﻿#if NET461
namespace Nerdbank.GitVersioning.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LibGit2Sharp;
    using Validation;
    using Xunit.Abstractions;

    public abstract class RepoTestBase : IDisposable
    {
        private readonly List<string> repoDirectories = new List<string>();

        public RepoTestBase(ITestOutputHelper logger)
        {
            Requires.NotNull(logger, nameof(logger));

            this.Logger = logger;
            this.RepoPath = this.CreateDirectoryForNewRepo();
        }

        protected ITestOutputHelper Logger { get; }

        protected Repository Repo { get; set; }

        protected string RepoPath { get; set; }

        protected Signature Signer => new Signature("a", "a@a.com", new DateTimeOffset(2015, 8, 2, 0, 0, 0, TimeSpan.Zero));

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected string CreateDirectoryForNewRepo()
        {
            string repoPath;
            do
            {
                repoPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            } while (Directory.Exists(repoPath));
            Directory.CreateDirectory(repoPath);

            this.repoDirectories.Add(repoPath);
            return repoPath;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Repo?.Dispose();
                foreach (string dir in this.repoDirectories)
                {
                    try
                    {
                        TestUtilities.DeleteDirectory(dir);
                    }
                    catch (IOException)
                    {
                        // This happens in AppVeyor a lot.
                    }
                }
            }
        }

        protected void InitializeSourceControl()
        {
            Repository.Init(this.RepoPath);
            this.Repo = new Repository(this.RepoPath);
            foreach (var file in this.Repo.RetrieveStatus().Untracked)
            {
                Commands.Stage(this.Repo, file.FilePath);
            }

            if (this.Repo.Index.Count > 0)
            {
                this.Repo.Commit("initial commit", this.Signer, this.Signer);
            }
        }

        protected void AddCommits(int count = 1)
        {
            Verify.Operation(this.Repo != null, "Repo has not been created yet.");
            for (int i = 1; i <= count; i++)
            {
                this.Repo.Commit($"filler commit {i}", this.Signer, this.Signer, new CommitOptions { AllowEmptyCommit = true });
            }
        }

        protected Commit WriteVersionTxtFile(string version = "1.2", string prerelease = "", string relativeDirectory = null)
        {
            if (relativeDirectory == null)
            {
                relativeDirectory = string.Empty;
            }

            string versionFilePath = Path.Combine(this.RepoPath, relativeDirectory, "version.txt");
            File.WriteAllText(versionFilePath, $"{version}\r\n{prerelease}");
            return this.CommitVersionFile(versionFilePath, $"{version}{prerelease}");
        }

        protected Commit WriteVersionFile(string version = "1.2", string prerelease = "", string relativeDirectory = null)
        {
            var versionData = VersionOptions.FromVersion(new System.Version(version), prerelease);
            return this.WriteVersionFile(versionData, relativeDirectory);
        }

        protected Commit WriteVersionFile(VersionOptions versionData, string relativeDirectory = null)
        {
            Requires.NotNull(versionData, nameof(versionData));

            if (relativeDirectory == null)
            {
                relativeDirectory = string.Empty;
            }

            string versionFilePath = VersionFile.SetVersion(Path.Combine(this.RepoPath, relativeDirectory), versionData);
            return this.CommitVersionFile(versionFilePath, versionData.Version?.ToString());
        }

        protected Commit CommitVersionFile(string versionFilePath, string version)
        {
            Requires.NotNullOrEmpty(versionFilePath, nameof(versionFilePath));
            Requires.NotNullOrEmpty(versionFilePath, nameof(versionFilePath));

            if (this.Repo != null)
            {
                Assumes.True(versionFilePath.StartsWith(this.RepoPath, StringComparison.OrdinalIgnoreCase));
                var relativeFilePath = versionFilePath.Substring(this.RepoPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Commands.Stage(this.Repo, relativeFilePath);
                if (Path.GetExtension(relativeFilePath) == ".json")
                {
                    string txtFilePath = relativeFilePath.Substring(0, relativeFilePath.Length - 4) + "txt";
                    if (!File.Exists(Path.Combine(this.RepoPath, txtFilePath)) && this.Repo.Index[txtFilePath] != null)
                    {
                        this.Repo.Index.Remove(txtFilePath);
                    }
                }

                return this.Repo.Commit($"Add/write {relativeFilePath} set to {version ?? "Inherited"}", this.Signer, this.Signer, new CommitOptions { AllowEmptyCommit = true });
            }

            return null;
        }
    }
}
#endif