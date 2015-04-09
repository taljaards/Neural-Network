﻿Imports NeuralNetwork.Activation

Module NetworkOperation
    Public Sub loadTrainingData()
        'load testing inputs
        Using MyReader As New Microsoft.VisualBasic.FileIO.TextFieldParser(Form1.tb_input.Text) 'todo: have separate input for training set and "real" data set
            MyReader.TextFieldType = FileIO.FieldType.Delimited
            MyReader.SetDelimiters(",")

            ReDim ANN.inputData(ANN.numInputLines - 1, ANN.numInputs - 1)
            Dim i As Integer = 0
            Dim currentRow As String()
            While Not MyReader.EndOfData
                Try
                    currentRow = MyReader.ReadFields()

                    Dim currentField As String
                    Dim tempRow(3) As Double
                    Dim j As Integer = 0
                    For Each currentField In currentRow
                        ANN.inputData(i, j) = currentField
                        j += 1
                    Next

                    i += 1
                Catch ex As Microsoft.VisualBasic.FileIO.MalformedLineException
                    MsgBox("Line " & ex.Message & "is not valid and will be skipped.")
                End Try
            End While
        End Using

        'load expected outputs
        Using MyReader As New Microsoft.VisualBasic.FileIO.TextFieldParser(Form1.tb_output.Text)
            MyReader.TextFieldType = FileIO.FieldType.Delimited
            MyReader.SetDelimiters(",")

            ReDim ANN.expectedOutputs(ANN.numOutputLines - 1, ANN.expectedOutputsPerLine - 1)
            Dim i As Integer = 0
            Dim currentRow As String()
            While Not MyReader.EndOfData
                Try
                    currentRow = MyReader.ReadFields()

                    Dim currentField As String
                    Dim j As Integer = 0
                    For Each currentField In currentRow
                        ANN.expectedOutputs(i, j) = currentField
                        j += 1
                    Next

                    i += 1
                Catch ex As Microsoft.VisualBasic.FileIO.MalformedLineException
                    MsgBox("Line " & ex.Message & "is not valid and will be skipped.")
                End Try
            End While
        End Using
    End Sub

    Private Sub layerCalculate(network As BackpropagationNetwork, layerIndex As Integer)
        Dim currentLayer As Layer = network.Layers(layerIndex)
        Dim prevLayer As Layer = network.Layers(layerIndex - 1)

        Dim sum(currentLayer.NeuronCount - 1) As Double

        'Pre-populate sums with bias values
        For i = 0 To currentLayer.NeuronCount - 1
            sum(i) = currentLayer.Bias(i)
        Next

        'calculate each node's sum
        For currentNeuronInCurrentLayer = 0 To currentLayer.NeuronCount - 1
            For currentNeuronInPrevLayer = 0 To prevLayer.NeuronCount - 1
                sum(currentNeuronInCurrentLayer) += prevLayer.Outputs(currentNeuronInPrevLayer) * prevLayer.Weights(currentNeuronInPrevLayer, currentNeuronInCurrentLayer)
            Next
        Next

        currentLayer.Inputs = sum

        'calcute the function value (output) using an activation function
        Dim functionValue(currentLayer.NeuronCount - 1) As Double
        For i = 0 To currentLayer.NeuronCount - 1
            functionValue(i) = ActivationFunctions.Evaluate(currentLayer.ActivationFunction, sum(i))
        Next

        currentLayer.Outputs = functionValue
    End Sub

    Public Sub networkCalculate(networkToCalculate As BackpropagationNetwork)
        Dim i As Integer = 0
        For Each layer In networkToCalculate.Layers
            If layer.LayerType = ILayer.LayerType_.Input Then
                'do nothing
            Else
                layerCalculate(networkToCalculate, i)
            End If

            i += 1
        Next
    End Sub

    Public Function trainNetwork(ByRef network As BackpropagationNetwork, learningRate As Double, momentum As Double) As Double
        'TODO: add epoch looping
        Dim exampleError(numInputLines - 1) As Double
        For ex = 0 To numInputLines - 1 'for each example

            'run the network once to get output values
            network.Layers(0).Outputs = Util.Array.GetRow(ex, inputData)
            networkCalculate(network)

            'do error calculation
            For l = network.LayerCount - 1 To 1 Step -1 'for each layer (exept input layer), calculate the error, starting from the back
                Dim currentLayer As Layer = network.Layers(l)
                Dim prevLayer As Layer = network.Layers(l - 1)

                If network.Layers(l).LayerType = ILayer.LayerType_.Output Then

                    For k = 0 To network.LastLayer.NeuronCount - 1 'for each output neuron
                        Dim diff As Double
                        diff = network.LastLayer.Outputs(k) - expectedOutputs(ex, k)

                        exampleError(ex) += diff ^ 2

                        Dim delta_k As Double
                        delta_k = diff * ActivationFunctions.EvaluateDerivative(network.Layers(l).ActivationFunction, network.Layers(l).Inputs(k)) 'the formula for delta_k

                        network.Layers(l).Deltas(k) = delta_k
                    Next

                Else 'hidden layer

                    For i = 0 To network.Layers(l).NeuronCount - 1 'for each neuron in the current layer

                        'for each neuron in the next layer (the layer nearer to the output), calculate the delta_j_tempSum (for use in calculating delta_j)
                        Dim delta_j_tempSum As Double = 0
                        For j = 0 To network.Layers(l + 1).NeuronCount - 1
                            delta_j_tempSum += network.Layers(l + 1).Deltas(j) * network.Layers(l).Weights(i, j)
                        Next

                        Dim delta_j As Double
                        delta_j = ActivationFunctions.EvaluateDerivative(network.Layers(l).ActivationFunction, network.Layers(l).Inputs(i)) * delta_j_tempSum 'the formula for delta_j

                        network.Layers(l).Deltas(i) = delta_j
                    Next

                End If

            Next


            'update the weights
            For layer = 1 To network.LayerCount - 1 'for each layer (except input layer)
                Dim currentLayer As Layer = network.Layers(layer)
                Dim prevLayer As Layer = network.Layers(layer - 1)
                'If network.Layers(layer).LayerType = ILayer.LayerType_.Input Then
                'do nothing, the input layer does not have weights (or const weights = 1)
                'Else

                'TODO: maak seker oor die 0 (begin van die video)v
                For i = 0 To prevLayer.NeuronCount - 1 'for each neuron in the previous layer

                    For j = 0 To currentLayer.NeuronCount - 1 'each neuron in the current layer

                        prevLayer.WeightDeltas(i, j) = learningRate * currentLayer.Deltas(j) * prevLayer.Outputs(i)

                        prevLayer.Weights(i, j) -= prevLayer.WeightDeltas(i, j) + momentum * prevLayer.PreviousWeightDeltas(i, j)

                        prevLayer.PreviousWeightDeltas(i, j) = prevLayer.WeightDeltas(i, j)
                    Next
                Next
            Next

            If Form1.chk_updateBias.Checked Then
                For layer = 1 To network.LayerCount - 1 'for each layer (except input layer)
                    For i = 0 To network.Layers(layer).NeuronCount - 1 'each neuron in the current layer

                        network.Layers(layer).BiasDeltas(i) = learningRate * network.Layers(layer).Deltas(i)

                        network.Layers(layer).Bias(i) -= network.Layers(layer).BiasDeltas(i) + momentum * network.Layers(layer).PreviousBiasDeltas(i)

                        network.Layers(layer).PreviousBiasDeltas(i) = network.Layers(layer).BiasDeltas(i)
                    Next
                Next
            End If
        Next

        'todo vv
        Dim exampleErrorSum As Double
        For i = 0 To numInputLines - 1
            exampleErrorSum += exampleError(i)
        Next

        Dim MSE As Double = exampleErrorSum / numInputLines
        Dim RMSE As Double = Math.Sqrt(MSE)

        Return RMSE
    End Function
End Module
