﻿namespace Suave

/// Inspired by https://github.com/NancyFx/Nancy/blob/45860c82e4df8e2d380997ddf1d19d61400fb145/src/Nancy/RequestHeaders.cs
module Headers =
  /// Parse a DateTime as given in the 'Date' Header field.
  let parseDateTime s =
    match System.DateTime.TryParseExact(s, "R", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None) with
    | true, v -> Some v
    | _ -> None

  /// Parse a decimal as given in a header field.
  let parseDecimal s =
    match System.Decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture) with
    | true, d -> Some d
    | _ -> None
  
  /// Parse a culture info as given in the 'Accept-Language' Header field.
  let parseCultureInfo =
    let cultureNames =
      new System.Collections.Generic.HashSet<string>(
        System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
        |> Seq.map (fun c -> c.Name), System.StringComparer.OrdinalIgnoreCase)

    let isValidCulture s =
      System.String.IsNullOrWhiteSpace s |> not &&
      cultureNames.Contains s

    let parseCultureInfo s =
      if isValidCulture s then
        System.Globalization.CultureInfo(s) |> Some
      else None

    parseCultureInfo

  /// Return the first header value with the given name.
  let getFirstHeader name ctx =
    ctx.request.headers
    |> Seq.tryFind (fst >> (=) name)
    |> Option.map snd

  /// Return all headers with the given name.
  let getHeader name ctx =
    ctx.request.headers
    |> Seq.filter (fst >> (=) name)
    |> Seq.map fst

  /// group headers by name and collect all headers in a dictionary.
  let getHeaders ctx =
    ctx.request.headers
    |> Seq.groupBy fst
    |> Seq.map (fun (k,v) -> k, Seq.map fst v)
    |> dict

  /// Split the given header values.
  let getSplitValues headers =
    headers
    |> Seq.collect (fun (x:string) -> x.Split([|','|], System.StringSplitOptions.RemoveEmptyEntries))
    |> Seq.map (fun x -> x.Trim())

  /// order the given split header values by their weight (given via 'q=')
  let getWeightedValues splitValues =
    splitValues
    |> Seq.map (fun (x:string) ->
      let mediaRange, sections =
        match x.Split([|';'|], System.StringSplitOptions.RemoveEmptyEntries)
              |> Seq.map (fun x -> x.Trim())
              |> Seq.toList with
        | mediaRange :: sections -> mediaRange, sections
        | _ -> failwith "expected at least one header!"

      let qVal, others =
        sections
        |> List.partition (fun x -> x.StartsWith("q=", System.StringComparison.OrdinalIgnoreCase))

      let quality =
        match qVal
              |> Seq.map (fun x -> x.Substring 2)
              |> Seq.choose parseDecimal
              //|> Seq.tryHead with // TODO: F# 4
              |> Seq.tryFind (fun _ -> true) with
        | Some d -> d
        | None -> 1m

      let mediaRange =
        Seq.append [mediaRange] others
        |> String.concat ";"
      mediaRange, quality)
    //|> Seq.sortByDescending snd // TODO: F# 4
    |> Seq.sortBy (fun (_, q) -> -q)