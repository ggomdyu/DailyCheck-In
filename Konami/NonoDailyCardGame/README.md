# How to use
1. Create a bare clone of this repository.
```
git clone --bare https://github.com/ggomdyu/DailyCheck-In.git
```

2. Create a new **private repository** on Github.
> You MUST create your repository as private because you will write your password on yml file!

3. Mirror-push your bare clone to your new repository.
>Replace `<your_repository_name>` with your new repository name and `<your_username>` with your actual Github username.
```
cd DailyCheck-In.git
git push --mirror git@github.com:<your_username>/<your_repository_name>.git
```

4. Replace `<your_email>` and `<your_password>` in RunNonoDailyCardGame.yml with your actual 
e-AMUSEMENT website's email and password.
