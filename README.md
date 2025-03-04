# ObjectSerializer

## 概要

C#のオブジェクトをJSONに相互変換し、保存可能にします。また、パスワードを指定して暗号化することができます。
シリアライズ可能なクラスは整数型、ブール値、文字列、配列、コレクション型、辞書型、その他単純なクラスなどです。

このプロジェクトで配布されているDLLはdotnet 5 C# 9.0環境でビルドされています。

ソース生成機能を利用していないため、やや速さは劣ります。

## 非Unity環境での導入

プロジェクト環境がdotnet 5.0以降であれば問題なく使用できると思います。

Visual Studio環境での例

1. ObjectSerializer.dllをダウンロードする。
2. 使用したいプロジェクトの参照マネージャーを開き、下の`参照(B)...`からObjectSerializer.dllを参照リストに加える。
3. チェックボックスにチェックをつける。

## Unity環境での導入

~~想定と違い~~まともに動かないため、サポート対象外とします。

## 使い方

`ObjectSerializer.Serializer.Serialize<T>(T obj, string? password)`を使用してインスタンスをJSONに変換します。このとき、`password`にnull以外を指定すれば、そのパスワードで暗号化されます。また、何も設定しなければ暗号化されていないJSONテキストが返されます。

元に戻す時は`ObjectSerializer.Serializer.Deserialize<T>(string serializedText, string? password)`を使用してインスタンスを復元します。このとき、`password`はシリアライズ時と同じものを利用してください(未設定時は空欄)。

```cs
using static ObjectSerializer.Serializer;

class Sample
{
  public static void Main()
  {
    // Create a new instance of TestRec
    var testRec = new TestRec(
        int.MaxValue,
        double.MaxValue,
        true,
        "Hello World",
        new List<int> { 1, 2, 3, 4, 5 },
        new Dictionary<string, int> { { "A", 1 }, { "B", 2 }, { "C", 3 } },
        new TestSubRec("Sub")
        );

    // Serialize without password
    string json1 = Serialize(testRec);

    // Serialize without password
    TestRec testRec1 = Deserialize<TestRec>(json1);

    // Serialize with password
    string json2 = Serialize(testRec, "Password");

    // Deserialize with password
    TestRec testRec2 = Deserialize<TestRec>(json2, "Password");
  }
}

// Sample record(class)
public record TestRec(
  long Int,
  double Float,
  bool Bool,
  string String,
  List<int> List,
  Dictionary<string, int> Dictionary,
  TestSubRec SubRec
){ }

public record TestSubRec(
  string Sub
){ }
```
